using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Application.Meta;
using Noovio.FrontPulse.Domain;
using Noovio.FrontPulse.Domain.Entities;
using Noovio.FrontPulse.Domain.Enums;

namespace Noovio.FrontPulse.Application.Ingestion;

public sealed class IngestionOptions
{
    public const string SectionName = "FrontPulse:Ingestion";

    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(6);
    public bool RunOnStartup { get; set; } = true;

    /// <summary>Taille du top retenu par spécialisation.</summary>
    public int TanksPerSpec { get; set; } = 100;

    /// <summary>
    /// Candidats lus dans le classement : une marge au-delà du top, car certains profils sont
    /// introuvables côté Blizzard ou joués aujourd'hui dans une autre spécialisation.
    /// </summary>
    public int CandidatesPerSpec { get; set; } = 130;

    public int MaxParallelism { get; set; } = 8;
}

public sealed record SpecIngestionResult(TankSpec Spec, int Candidates, int Profiles);

public sealed record IngestionReport(string Season, DateTimeOffset StartedAtUtc, TimeSpan Duration, IReadOnlyList<SpecIngestionResult> Specs);

/// <summary>Commande d'ingestion complète : classement → profils → agrégation → snapshots.</summary>
public sealed class TankMetaIngestionService(
    ILeaderboardSource leaderboard,
    ICharacterLoadoutSource characters,
    IMediaSource media,
    ITalentTreeSource talentTrees,
    ITankMetaStore store,
    TankMetaAggregatorService aggregator,
    IOptions<IngestionOptions> options,
    TimeProvider time,
    ILogger<TankMetaIngestionService> logger)
{
    private readonly IngestionOptions _options = options.Value;

    /// <param name="progress">Avancement tenu à jour pendant l'exécution (suivi par la page d'admin), facultatif.</param>
    public async Task<IngestionReport> RunAsync(CancellationToken ct, IngestionProgress? progress = null)
    {
        var startedAt = time.GetUtcNow();
        var season = await leaderboard.GetCurrentSeasonAsync(ct);
        var candidates = await leaderboard.GetTopTanksAsync(season, _options.CandidatesPerSpec, ct, progress);
        progress?.LeaderboardRead(candidates.Values.Sum(c => c.Count), time.GetUtcNow());
        var results = new List<SpecIngestionResult>();

        foreach (var info in TankSpecs.All)
        {
            progress?.SpecStarted(info.Spec);
            var tanks = candidates.GetValueOrDefault(info.Spec) ?? [];
            var profiles = await ResolveAsync(season, info.Spec, tanks, progress, ct);
            results.Add(new SpecIngestionResult(info.Spec, tanks.Count, profiles.Count));

            if (profiles.Count == 0)
            {
                // On garde le snapshot précédent plutôt que d'afficher une page vide.
                logger.LogWarning("Aucun profil résolu pour {Spec} ({Candidates} candidats), snapshot conservé", info.Spec, tanks.Count);
                progress?.SpecDone();
                continue;
            }

            await store.ReplaceAsync(season, info.Spec, profiles, await BuildSnapshotAsync(info.Spec, season, profiles, ct), ct);

            logger.LogInformation("{Spec} : {Profiles}/{Candidates} profils agrégés", info.Spec, profiles.Count, tanks.Count);
            progress?.SpecDone();
        }

        return new IngestionReport(season, startedAt, time.GetUtcNow() - startedAt, results);
    }

    /// <summary>
    /// Recalcule les snapshots de la saison à partir des profils déjà en base, sans relire le classement
    /// ni les profils (quelques secondes au lieu d'une demi-heure) : utile après un changement des règles d'agrégation.
    /// </summary>
    public async Task<IngestionReport> RebuildSnapshotsAsync(CancellationToken ct)
    {
        var startedAt = time.GetUtcNow();
        var season = await leaderboard.GetCurrentSeasonAsync(ct);
        var results = new List<SpecIngestionResult>();

        foreach (var info in TankSpecs.All)
        {
            var profiles = await store.GetProfilesAsync(season, info.Spec, ct);
            results.Add(new SpecIngestionResult(info.Spec, profiles.Count, profiles.Count));
            if (profiles.Count > 0)
            {
                await store.ReplaceSnapshotAsync(await BuildSnapshotAsync(info.Spec, season, profiles, ct), ct);
            }
        }

        return new IngestionReport(season, startedAt, time.GetUtcNow() - startedAt, results);
    }

    private async Task<SpecMetaSnapshot> BuildSnapshotAsync(TankSpec spec, string season, IReadOnlyList<TankProfile> profiles, CancellationToken ct)
    {
        var snapshot = aggregator.Aggregate(spec, season, profiles, time.GetUtcNow());
        var itemIcons = await media.GetItemIconsAsync(TankMetaAggregatorService.ItemIds(snapshot), ct);
        var spellIcons = await media.GetSpellIconsAsync(TankMetaAggregatorService.SpellIds(snapshot), ct);
        snapshot = TankMetaAggregatorService.WithIcons(snapshot, itemIcons, spellIcons);
        return TankMetaAggregatorService.WithTalentTree(snapshot, await talentTrees.GetLayoutAsync(spec, ct));
    }

    private async Task<List<TankProfile>> ResolveAsync(string season, TankSpec spec, IReadOnlyList<LeaderboardTank> tanks, IngestionProgress? progress, CancellationToken ct)
    {
        var resolved = new TankProfile?[tanks.Count];
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = _options.MaxParallelism, CancellationToken = ct };

        await Parallel.ForEachAsync(Enumerable.Range(0, tanks.Count), parallel, async (i, token) =>
        {
            var tank = tanks[i];
            try
            {
                var loadout = await characters.GetLoadoutAsync(tank, token);

                // Joué aujourd'hui dans une autre spécialisation : l'équipement et les caractéristiques ne sont plus ceux du tank.
                if (loadout is not null && loadout.ActiveSpecId == (int)spec)
                {
                    resolved[i] = ToProfile(season, tank, loadout);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !token.IsCancellationRequested)
            {
                // Erreur réseau ou timeout sur un personnage : il est ignoré, les autres continuent.
                logger.LogWarning(ex, "Profil {Region}/{Realm}/{Name} ignoré", tank.Region, tank.RealmSlug, tank.Name);
            }
            finally
            {
                progress?.ProfileRead();
            }
        });

        // Le classement d'origine est conservé ; les rangs sont renumérotés après exclusions.
        var profiles = resolved.OfType<TankProfile>().Take(_options.TanksPerSpec).ToList();
        for (var i = 0; i < profiles.Count; i++)
        {
            profiles[i].Rank = i + 1;
        }

        return profiles;
    }

    private TankProfile ToProfile(string season, LeaderboardTank tank, CharacterLoadout loadout) => new()
    {
        Season = season,
        Spec = tank.Spec,
        RaiderIoCharacterId = tank.RaiderIoCharacterId,
        Region = tank.Region,
        RealmSlug = tank.RealmSlug,
        Name = tank.Name,
        ProfileUrl = tank.ProfileUrl,
        BestRunScore = tank.BestRunScore,
        BestKeyLevel = tank.BestKeyLevel,
        // La chaîne vue sur la clé classée reflète le build M+ ; le build actif Blizzard sert de repli.
        LoadoutCode = tank.LoadoutCode ?? loadout.LoadoutCode,
        HeroTree = loadout.HeroTree,
        ItemLevel = loadout.ItemLevel,
        Gear = [.. loadout.Gear],
        Stats = loadout.Stats,
        Talents = [.. loadout.Talents],
        FetchedAtUtc = time.GetUtcNow()
    };
}
