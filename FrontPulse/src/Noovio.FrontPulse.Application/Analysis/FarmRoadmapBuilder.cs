using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Application.Meta;
using Noovio.FrontPulse.Domain.Entities;
using Noovio.FrontPulse.Domain.Enums;

namespace Noovio.FrontPulse.Application.Analysis;

/// <summary>
/// Quoi farmer et où, sans E/S : chaque emplacement est comparé aux pièces les plus portées du top, puis les pièces
/// visées sont regroupées par instance d'après le Journal d'aventure. L'ordre est un score explicite :
/// poids de l'emplacement, popularité dans le top, écart de niveau d'objet, apport aux caractéristiques à corriger.
/// </summary>
public static class FarmRoadmapBuilder
{
    /// <summary>Une pièce portée par au moins cette part du top est considérée comme un bon choix : on l'améliore, on ne la remplace pas.</summary>
    private const double KeepShare = 0.1;

    /// <summary>Une pièce n'est visée que si elle est au moins aussi répandue dans le top.</summary>
    private const double TargetShare = 0.15;

    /// <summary>Écart de niveau d'objet sous la médiane du top à partir duquel une pièce est à améliorer ou remplacer.</summary>
    private const int IlvlGap = 6;

    /// <summary>Un donjon 2 niveaux sous la médiane des meilleures clés est « en retard » : même repère que l'analyse M+.</summary>
    private const int WeakKeyGap = 2;

    public static FarmRoadmap Build(
        IReadOnlyList<GearPiece> gear,
        SpecMetaSnapshot meta,
        IReadOnlyList<TankProfile> top,
        IReadOnlyList<StatReview> stats,
        MythicPlusProfile? mythicPlus,
        EncounterJournal? journal,
        IReadOnlyDictionary<int, string> icons)
    {
        var n = Math.Max(1, top.Count);
        var low = stats.Where(s => s.Percent < s.Top.P25Percent).Select(s => s.Stat).ToHashSet();
        var high = stats.Where(s => s.Percent > s.Top.P75Percent).Select(s => s.Stat).ToHashSet();
        var sources = Sources(journal);
        var trinketTargets = TrinketPlanner.Targets(gear, top, meta);

        var worn = gear.Select(g => g.ItemId).ToHashSet();
        var claimed = new HashSet<int>();
        var replacements = new List<(FarmTarget Target, LootSource? Source)>();
        var upgrades = new List<FarmTarget>();

        // Les emplacements qui pèsent le plus choisissent d'abord : sur deux bijoux, la première pièce visée est la plus portée.
        foreach (var piece in gear.OrderByDescending(g => SlotWeight(g.Slot)).ThenBy(g => g.ItemLevel))
        {
            var picks = meta.Slots.FirstOrDefault(s => s.Slot == piece.Slot)?.Items ?? [];
            var topPieces = top.SelectMany(p => p.Gear).Where(g => g.Slot == piece.Slot).ToList();
            var slotIlvl = (int)Math.Round(TankMetaAggregatorService.Median(topPieces.Select(g => (double)g.ItemLevel)));
            var share = (double)top.Count(p => p.Gear.Any(g => g.ItemId == piece.ItemId)) / n;
            var gap = slotIlvl - piece.ItemLevel;

            var pick = piece.Slot == GearSlot.Trinket
                ? picks.FirstOrDefault(p => trinketTargets.GetValueOrDefault(piece.ItemId) == p.ItemId)
                : picks.FirstOrDefault(p => !worn.Contains(p.ItemId) && !claimed.Contains(p.ItemId) && p.Share >= TargetShare && p.Share > share);
            if (share >= KeepShare || pick is null)
            {
                if (gap >= IlvlGap)
                {
                    upgrades.Add(new FarmTarget(piece.Slot, piece.ItemId, piece.Name, icons.GetValueOrDefault(piece.ItemId), share, slotIlvl, piece.Name, piece.ItemLevel, null, false, [], [],
                        SlotWeight(piece.Slot) + gap / 10.0));
                }

                continue;
            }

            claimed.Add(pick.ItemId);
            var details = topPieces.FirstOrDefault(g => g.ItemId == pick.ItemId);
            var pickStats = (details?.Stats ?? []).Select(s => s.Stat).Distinct().ToList();
            var fixes = pickStats.Where(low.Contains).ToList();
            var priority = SlotWeight(piece.Slot) * (1 + pick.Share) + Math.Max(0, pick.MedianItemLevel - piece.ItemLevel) / 10.0
                           + fixes.Count * 0.5 - pickStats.Count(high.Contains) * 0.5;
            var source = sources.GetValueOrDefault(pick.ItemId);

            replacements.Add((
                new FarmTarget(piece.Slot, pick.ItemId, pick.Name, pick.IconUrl ?? icons.GetValueOrDefault(pick.ItemId), pick.Share, pick.MedianItemLevel, piece.Name, piece.ItemLevel,
                    source?.Boss, details?.IsEmbellished == true, pickStats, fixes, priority),
                source));
        }

        var stops = replacements
            .Where(r => r.Source is not null)
            .GroupBy(r => r.Source!.Instance.Id)
            .Select(g => Stop(g.First().Source!.Instance, g.Select(x => x.Target).ToList(), mythicPlus))
            .OrderByDescending(s => s.Priority)
            .ToList();

        return new FarmRoadmap(
            stops,
            replacements.Where(r => r.Source is null).Select(r => r.Target).OrderByDescending(t => t.Priority).ToList(),
            upgrades.OrderByDescending(t => t.Priority).ToList(),
            journal is not null);
    }

    private static FarmStop Stop(JournalInstance instance, List<FarmTarget> targets, MythicPlusProfile? mythicPlus)
    {
        var run = mythicPlus?.BestRuns.FirstOrDefault(r => r.Dungeon == instance.RaiderIoName);
        var missing = instance.Kind == InstanceKind.Dungeon && mythicPlus?.MissingDungeons.Contains(instance.RaiderIoName) == true;
        var median = mythicPlus is null ? 0 : TankMetaAggregatorService.Median(mythicPlus.BestRuns.Select(r => (double)r.Level));
        var weak = run is not null && run.Level <= median - WeakKeyGap;

        // Le même donjon rapporte aussi du score : à butin égal, il passe devant.
        var priority = targets.Sum(t => t.Priority) + (missing ? 1 : weak ? 0.5 : 0);
        return new FarmStop(instance.Name, instance.Kind, targets.OrderByDescending(t => t.Priority).ToList(), priority, run?.Level, weak, missing);
    }

    /// <summary>Un objet qui tombe à plusieurs endroits : le donjon d'abord, accessible sans groupe de raid.</summary>
    private static Dictionary<int, LootSource> Sources(EncounterJournal? journal)
    {
        var sources = new Dictionary<int, LootSource>();
        foreach (var instance in (journal?.Instances ?? []).OrderBy(i => i.Kind))
        {
            foreach (var boss in instance.Bosses)
            {
                foreach (var itemId in boss.ItemIds)
                {
                    sources.TryAdd(itemId, new LootSource(instance, boss.Name));
                }
            }
        }

        return sources;
    }

    private static double SlotWeight(GearSlot slot) => slot switch
    {
        GearSlot.Trinket => 3,
        GearSlot.MainHand => 2.5,
        GearSlot.OffHand => 1.5,
        _ => 1
    };

    private sealed record LootSource(JournalInstance Instance, string Boss);
}

public sealed record FarmRoadmap(
    IReadOnlyList<FarmStop> Stops,
    IReadOnlyList<FarmTarget> Elsewhere,
    IReadOnlyList<FarmTarget> Upgrades,
    bool HasJournal);

/// <summary>Une instance à faire, avec le butin à y chercher. <see cref="BestKeyLevel"/>, <see cref="KeyIsWeak"/>, <see cref="KeyIsMissing"/> : ta progression M+ dessus.</summary>
public sealed record FarmStop(string Instance, InstanceKind Kind, IReadOnlyList<FarmTarget> Targets, double Priority, int? BestKeyLevel, bool KeyIsWeak, bool KeyIsMissing);

/// <summary>
/// Une pièce visée. <see cref="CurrentName"/> : la pièce portée qu'elle remplace ; <see cref="Stats"/> : ses secondaires ;
/// <see cref="Fixes"/> : celles qui font partie des caractéristiques sous la fourchette du top.
/// </summary>
public sealed record FarmTarget(
    GearSlot Slot, int ItemId, string Name, string? IconUrl, double TopShare, int TargetItemLevel, string CurrentName, int CurrentItemLevel,
    string? Boss, bool IsCrafted, IReadOnlyList<SecondaryStat> Stats, IReadOnlyList<SecondaryStat> Fixes, double Priority);
