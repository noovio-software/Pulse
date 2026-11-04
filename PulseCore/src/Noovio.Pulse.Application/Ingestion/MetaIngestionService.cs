using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Meta;
using Noovio.Pulse.Domain.Roles;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Enums;

namespace Noovio.Pulse.Application.Ingestion;

public sealed class IngestionOptions
{
    public const string SectionName = "Ingestion";

    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(6);
    public bool RunOnStartup { get; set; } = true;

    /// <summary>Taille du top retenu par spécialisation.</summary>
    public int PlayersPerSpec { get; set; } = 100;

    /// <summary>
    /// Candidats lus dans le classement : une marge au-delà du top, car certains profils sont
    /// introuvables côté Blizzard ou joués aujourd'hui dans une autre spécialisation.
    /// </summary>
    public int CandidatesPerSpec { get; set; } = 130;

    public int MaxParallelism { get; set; } = 8;
}

public sealed record SpecIngestionResult(WowSpec Spec, int Candidates, int Profiles);

public sealed record IngestionReport(string Season, DateTimeOffset StartedAtUtc, TimeSpan Duration, IReadOnlyList<SpecIngestionResult> Specs);

/// <summary>Commande d'ingestion complète : classement → profils → agrégation → snapshots.</summary>
public sealed class MetaIngestionService(
    ILeaderboardSource leaderboard,
    ICharacterLoadoutSource characters,
    PulseRole role,
    IMediaSource media,
    ITalentTreeSource talentTrees,
    IMetaStore store,
    MetaAggregatorService aggregator,
    IOptions<IngestionOptions> options,
    TimeProvider time,
    ILogger<MetaIngestionService> logger)
{
    private readonly IngestionOptions _options = options.Value;

    /// <param name="progress">Avancement tenu à jour pendant l'exécution (suivi par la page d'admin), facultatif.</param>
    public async Task<IngestionReport> RunAsync(CancellationToken ct, IngestionProgress? progress = null)
    {
        var startedAt = time.GetUtcNow();
        var season = await leaderboard.GetCurrentSeasonAsync(ct);
        var candidates = await leaderboard.GetTopPlayersAsync(season, _options.CandidatesPerSpec, ct, progress);
        progress?.LeaderboardRead(candidates.Values.Sum(c => c.Count), time.GetUtcNow());
        var results = new List<SpecIngestionResult>();

        foreach (var info in role.Specs)
        {
            progress?.SpecStarted(info.Spec);
            var players = candidates.GetValueOrDefault(info.Spec) ?? [];
            var profiles = await ResolveAsync(season, info.Spec, players, progress, ct);
            results.Add(new SpecIngestionResult(info.Spec, players.Count, profiles.Count));

            if (profiles.Count == 0)
            {
                // On garde le snapshot précédent plutôt que d'afficher une page vide.
                logger.LogWarning("Aucun profil résolu pour {Spec} ({Candidates} candidats), snapshot conservé", info.Spec, players.Count);
                progress?.SpecDone();
                continue;
            }

            await store.ReplaceAsync(season, info.Spec, profiles, await BuildSnapshotAsync(info.Spec, season, profiles, ct), ct);

            logger.LogInformation("{Spec} : {Profiles}/{Candidates} profils agrégés", info.Spec, profiles.Count, players.Count);
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

        foreach (var info in role.Specs)
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

    private async Task<SpecMetaSnapshot> BuildSnapshotAsync(WowSpec spec, string season, IReadOnlyList<PlayerProfile> profiles, CancellationToken ct)
    {
        var snapshot = aggregator.Aggregate(spec, season, profiles, time.GetUtcNow());
        var itemIcons = await media.GetItemIconsAsync(MetaAggregatorService.ItemIds(snapshot), ct);
        var spellIcons = await media.GetSpellIconsAsync(MetaAggregatorService.SpellIds(snapshot), ct);
        snapshot = MetaAggregatorService.WithIcons(snapshot, itemIcons, spellIcons);
        return MetaAggregatorService.WithTalentTree(snapshot, await talentTrees.GetLayoutAsync(spec, ct));
    }

    private async Task<List<PlayerProfile>> ResolveAsync(string season, WowSpec spec, IReadOnlyList<LeaderboardPlayer> players, IngestionProgress? progress, CancellationToken ct)
    {
        var resolved = new PlayerProfile?[players.Count];
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = _options.MaxParallelism, CancellationToken = ct };

        await Parallel.ForEachAsync(Enumerable.Range(0, players.Count), parallel, async (i, token) =>
        {
            var player = players[i];
            try
            {
                var loadout = await characters.GetLoadoutAsync(player, token);

                // Joué aujourd'hui dans une autre spécialisation : l'équipement et les caractéristiques ne sont plus ceux de celle-ci.
                if (loadout is not null && loadout.ActiveSpecId == (int)spec)
                {
                    resolved[i] = ToProfile(season, player, loadout);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !token.IsCancellationRequested)
            {
                // Erreur réseau ou timeout sur un personnage : il est ignoré, les autres continuent.
                logger.LogWarning(ex, "Profil {Region}/{Realm}/{Name} ignoré", player.Region, player.RealmSlug, player.Name);
            }
            finally
            {
                progress?.ProfileRead();
            }
        });

        // Le classement d'origine est conservé ; les rangs sont renumérotés après exclusions.
        var profiles = resolved.OfType<PlayerProfile>().Take(_options.PlayersPerSpec).ToList();
        for (var i = 0; i < profiles.Count; i++)
        {
            profiles[i].Rank = i + 1;
        }

        return profiles;
    }

    private PlayerProfile ToProfile(string season, LeaderboardPlayer player, CharacterLoadout loadout) => new()
    {
        Season = season,
        Spec = player.Spec,
        RaiderIoCharacterId = player.RaiderIoCharacterId,
        Region = player.Region,
        RealmSlug = player.RealmSlug,
        Name = player.Name,
        ProfileUrl = player.ProfileUrl,
        BestRunScore = player.BestRunScore,
        BestKeyLevel = player.BestKeyLevel,
        // La chaîne vue sur la clé classée reflète le build M+ ; le build actif Blizzard sert de repli.
        LoadoutCode = player.LoadoutCode ?? loadout.LoadoutCode,
        HeroTree = loadout.HeroTree,
        ItemLevel = loadout.ItemLevel,
        Gear = [.. loadout.Gear],
        Stats = loadout.Stats,
        Talents = [.. loadout.Talents],
        FetchedAtUtc = time.GetUtcNow()
    };
}
