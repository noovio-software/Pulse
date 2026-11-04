using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Infrastructure.RaiderIo;

namespace Noovio.FrontPulse.Infrastructure.Blizzard;

/// <summary>
/// Journal d'aventure des instances de la saison : la liste vient de Raider.io (donjons de la saison, raids de l'extension),
/// le contenu de la Game Data API. Une cinquantaine d'appels, d'où le cache d'une journée, partagé par toutes les analyses.
/// </summary>
public sealed class BlizzardJournalSource(BlizzardClient client, RaiderIoClient raiderIo, IDistributedCache cache) : IEncounterJournalSource
{
    private static readonly DistributedCacheEntryOptions CacheOptions = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1) };

    public async Task<EncounterJournal?> GetAsync(string season, CancellationToken ct)
    {
        var key = $"journal:{season}";
        if (await cache.GetStringAsync(key, ct) is { } cached)
        {
            return JsonSerializer.Deserialize<EncounterJournal>(cached);
        }

        try
        {
            var journal = await LoadAsync(season, ct);
            if (journal is not null)
            {
                await cache.SetStringAsync(key, JsonSerializer.Serialize(journal), CacheOptions, ct);
            }

            return journal;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // Journal ou Raider.io indisponible : l'analyse se fait sans les sources de butin.
            return null;
        }
    }

    private async Task<EncounterJournal?> LoadAsync(string season, CancellationToken ct)
    {
        var dungeons = (await raiderIo.GetMythicPlusStaticDataAsync(ct))?.Seasons.FirstOrDefault(s => s.Slug == season)?.Dungeons ?? [];
        var raids = (await raiderIo.GetRaidingStaticDataAsync(ct))?.Raids ?? [];
        if (await client.GetJournalInstanceIndexAsync(ct) is not { } index)
        {
            return null;
        }

        var ids = index.Instances.GroupBy(i => Normalize(i.Name)).ToDictionary(g => g.Key, g => g.Max(i => i.Id));
        var wanted = dungeons.Select(d => (d.Name, Kind: InstanceKind.Dungeon))
            .Concat(raids.Select(r => (r.Name, Kind: InstanceKind.Raid)))
            .Where(w => ids.ContainsKey(Normalize(w.Name)))
            .ToList();

        var instances = new List<JournalInstance>();
        foreach (var (name, kind) in wanted)
        {
            if (await client.GetJournalInstanceAsync(ids[Normalize(name)], ct) is { } instance)
            {
                instances.Add(new JournalInstance(instance.Id, instance.Name, name, kind, await BossesAsync(instance, ct)));
            }
        }

        return instances.Count == 0 ? null : new EncounterJournal(instances);
    }

    private async Task<IReadOnlyList<JournalBoss>> BossesAsync(BzJournalInstance instance, CancellationToken ct)
    {
        var bosses = new ConcurrentDictionary<int, JournalBoss>();
        await Parallel.ForEachAsync(instance.Encounters, new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct }, async (reference, token) =>
        {
            if (await client.GetJournalEncounterAsync(reference.Id, token) is { } encounter)
            {
                bosses[reference.Id] = new JournalBoss(
                    encounter.Id,
                    encounter.Name,
                    encounter.Items.Select(i => i.Item.Id).Distinct().ToList(),
                    Abilities(encounter.Sections, tankHeading: false).ToList());
            }
        });

        // Dans l'ordre du journal, celui de la progression dans l'instance.
        return instance.Encounters.Where(e => bosses.ContainsKey(e.Id)).Select(e => bosses[e.Id]).ToList();
    }

    private static IEnumerable<JournalAbility> Abilities(IEnumerable<BzJournalSection> sections, bool tankHeading)
    {
        foreach (var section in sections)
        {
            var underTank = tankHeading || section.Title?.Contains("tank", StringComparison.OrdinalIgnoreCase) == true;
            if (!string.IsNullOrWhiteSpace(section.Title) && !string.IsNullOrWhiteSpace(section.BodyText))
            {
                yield return new JournalAbility(section.Title, section.BodyText, underTank);
            }

            foreach (var child in Abilities(section.Sections, underTank))
            {
                yield return child;
            }
        }
    }

    /// <summary>Tolère les écarts de ponctuation et de casse entre Raider.io et Blizzard.</summary>
    private static string Normalize(string? name) =>
        new((name ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
