using Noovio.Pulse.Application.Meta;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Enums;

namespace Noovio.Pulse.Application.Analysis;

/// <summary>
/// Gemmes et enchantements face au top, puis la meilleure série de changements pour ramener les
/// caractéristiques dans la fourchette du top. Sans E/S. Les pourcentages projetés sont une estimation :
/// la conversion indice → % est mesurée sur le top, sans tenir compte des rendements décroissants.
/// </summary>
public static class GemEnchantOptimizer
{
    /// <summary>Une gemme ou un enchantement moins répandu que ça dans le top n'est ni proposé, ni considéré comme courant.</summary>
    private const double RareShare = 0.05;

    /// <summary>Un enchantement différent n'est signalé que si le choix du top fait au moins cette part, et le sien moins que <see cref="RareShare"/>.</summary>
    private const double DominantEnchantShare = 0.3;

    /// <summary>Une châsse ou un enchantement est attendu sur un emplacement quand la majorité du top en a un.</summary>
    private const double ExpectedShare = 0.5;

    /// <summary>Gain minimal de l'objectif pour proposer de remplacer une gemme déjà posée : pas de dépense pour rien.</summary>
    private const double MinImprovement = 0.01;

    private const int MaxSwaps = 8;

    /// <summary>Échantillon minimal pour mesurer la conversion indice → % par régression.</summary>
    private const int MinRegressionSample = 5;

    public static GemEnchantReview Review(
        IReadOnlyList<GearPiece> gear,
        StatSheet? sheet,
        SpecMetaSnapshot meta,
        IReadOnlyList<PlayerProfile> top,
        IReadOnlyDictionary<int, string> icons)
    {
        var topPieces = top.SelectMany(p => p.Gear).ToList();
        var topHasDetails = topPieces.Any(g => g.Sockets is not null);
        var catalog = TopGems(topPieces, icons);
        var enchants = TopEnchants(topPieces);

        var plan = sheet is null || meta.Stats.Count == 0
            ? new SwapPlan([], [])
            : Optimize(gear, sheet, meta, top, catalog, enchants);

        var sockets = gear
            .SelectMany(piece => (piece.Sockets ?? []).Select((socket, index) => ReviewSocket(piece, socket, index, catalog, plan, topHasDetails, icons)))
            .ToList();

        return new GemEnchantReview(
            Breakdown(gear, top),
            sockets,
            ReviewEnchants(gear, top, enchants, plan),
            catalog.Take(5).ToList(),
            plan.Swaps,
            plan.Projection,
            MissingSockets(gear, top),
            topHasDetails);
    }

    // --- Ce que porte le top ------------------------------------------------

    private static List<GemUsage> TopGems(List<GearPiece> topPieces, IReadOnlyDictionary<int, string> icons)
    {
        var gems = topPieces.SelectMany(g => g.Sockets ?? []).Where(s => s.GemId is not null).ToList();
        return gems
            .GroupBy(s => s.GemId!.Value)
            .Select(g => new GemUsage(g.Key, g.First().GemName ?? $"#{g.Key}", icons.GetValueOrDefault(g.Key), (double)g.Count() / gems.Count, StatText.Parse(g.First().Display)))
            .OrderByDescending(g => g.Share)
            .ThenBy(g => g.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static List<EnchantUsage> TopEnchants(List<GearPiece> topPieces) =>
        topPieces
            .SelectMany(g => g.Enchantments.Select(e => (g.Slot, Text: e)))
            .GroupBy(e => e.Slot)
            .SelectMany(slot => slot
                .GroupBy(e => StatText.Label(e.Text))
                .Select(g => new EnchantUsage(slot.Key, StatText.ShortLabel(g.Key), (double)g.Count() / slot.Count(), StatText.Parse(g.Key))))
            .OrderByDescending(e => e.Share)
            .ToList();

    // --- Revue pièce par pièce ----------------------------------------------

    private static SocketReview ReviewSocket(
        GearPiece piece, GemSocket socket, int index, List<GemUsage> catalog, SwapPlan plan, bool topHasDetails, IReadOnlyDictionary<int, string> icons)
    {
        var stats = StatText.Parse(socket.Display);
        var share = catalog.FirstOrDefault(g => g.GemId == socket.GemId)?.Share ?? 0;
        var swap = plan.Swaps.FirstOrDefault(s => s.Kind != SwapKind.Enchant && s.ItemId == piece.ItemId && s.SocketIndex == index);
        var verdict = socket.GemId is null ? Verdict.Problem
            : swap is not null || (topHasDetails && share < RareShare) ? Verdict.Improve
            : Verdict.Good;

        return new SocketReview(
            piece.Slot, piece.ItemId, piece.Name, socket.GemId, socket.GemName,
            socket.GemId is { } id ? icons.GetValueOrDefault(id) : null,
            stats, share, verdict, swap);
    }

    private static List<EnchantReview> ReviewEnchants(IReadOnlyList<GearPiece> gear, IReadOnlyList<PlayerProfile> top, List<EnchantUsage> enchants, SwapPlan plan)
    {
        var n = Math.Max(1, top.Count);
        return gear
            .Select(piece =>
            {
                var usage = enchants.Where(e => e.Slot == piece.Slot).ToList();
                var expected = (double)top.Count(p => p.Gear.Any(g => g.Slot == piece.Slot && g.Enchantments.Count > 0)) / n >= ExpectedShare;
                var label = piece.Enchantments.FirstOrDefault() is { } text ? StatText.ShortLabel(text) : null;
                if (label is null && !expected)
                {
                    return null;
                }

                var best = usage.FirstOrDefault();
                var mine = usage.FirstOrDefault(e => e.Label == label);
                var swap = plan.Swaps.FirstOrDefault(s => s.Kind == SwapKind.Enchant && s.ItemId == piece.ItemId);
                var rare = (mine?.Share ?? 0) < RareShare && best is { Share: >= DominantEnchantShare };
                var verdict = label is null ? Verdict.Problem : swap is not null || rare ? Verdict.Improve : Verdict.Good;

                return new EnchantReview(
                    piece.Slot, piece.ItemId, piece.Name, label,
                    piece.Enchantments.SelectMany(StatText.Parse).ToList(),
                    mine?.Share ?? 0,
                    best?.Label == label ? null : best?.Label,
                    best?.Share ?? 0,
                    verdict,
                    swap);
            })
            .OfType<EnchantReview>()
            .ToList();
    }

    private static List<GearSlot> MissingSockets(IReadOnlyList<GearPiece> gear, IReadOnlyList<PlayerProfile> top)
    {
        var n = Math.Max(1, top.Count);
        return gear.Select(g => g.Slot).Distinct()
            .Where(slot => gear.Where(g => g.Slot == slot).All(g => (g.Sockets?.Count ?? 0) == 0))
            .Where(slot => (double)top.Count(p => p.Gear.Any(g => g.Slot == slot && g.Sockets?.Count > 0)) / n >= ExpectedShare)
            .ToList();
    }

    // --- D'où viennent les caractéristiques ---------------------------------

    private static List<StatBreakdown> Breakdown(IReadOnlyList<GearPiece> gear, IReadOnlyList<PlayerProfile> top)
    {
        var withDetails = top.Where(p => p.Gear.Any(g => g.Stats is not null)).ToList();
        return Enum.GetValues<SecondaryStat>()
            .Select(stat => new StatBreakdown(
                stat,
                ItemRating(gear, stat),
                GemRating(gear, stat),
                EnchantRating(gear, stat),
                MetaAggregatorService.Median(withDetails.Select(p => (double)ItemRating(p.Gear, stat))),
                MetaAggregatorService.Median(withDetails.Select(p => (double)GemRating(p.Gear, stat))),
                MetaAggregatorService.Median(withDetails.Select(p => (double)EnchantRating(p.Gear, stat)))))
            .ToList();
    }

    private static int ItemRating(IEnumerable<GearPiece> gear, SecondaryStat stat) =>
        gear.SelectMany(g => g.Stats ?? []).Where(s => s.Stat == stat).Sum(s => s.Value);

    private static int GemRating(IEnumerable<GearPiece> gear, SecondaryStat stat) =>
        gear.SelectMany(g => g.Sockets ?? []).SelectMany(s => StatText.Parse(s.Display)).Where(s => s.Stat == stat).Sum(s => s.Value);

    private static int EnchantRating(IEnumerable<GearPiece> gear, SecondaryStat stat) =>
        gear.SelectMany(g => g.Enchantments).SelectMany(StatText.Parse).Where(s => s.Stat == stat).Sum(s => s.Value);

    // --- Optimisation -------------------------------------------------------

    /// <summary>
    /// Recherche gloutonne : à chaque tour, le changement qui rapproche le plus les quatre caractéristiques de la
    /// fourchette du top ; chaque châsse ou enchantement ne change qu'une fois. Seuls sont proposés des choix réellement
    /// portés par le top. Les gemmes sans caractéristique lisible (effet unique) ne sont jamais remplacées.
    /// </summary>
    private static SwapPlan Optimize(
        IReadOnlyList<GearPiece> gear, StatSheet sheet, SpecMetaSnapshot meta, IReadOnlyList<PlayerProfile> top, List<GemUsage> catalog, List<EnchantUsage> enchants)
    {
        var rates = meta.Stats.ToDictionary(s => s.Stat, s => PercentPerRating(s, top));
        var percents = meta.Stats.ToDictionary(s => s.Stat, s => sheet.Get(s.Stat).Percent);
        var ranges = meta.Stats.ToDictionary(s => s.Stat);

        var options = new List<Option>();
        foreach (var piece in gear)
        {
            foreach (var (socket, index) in (piece.Sockets ?? []).Select((s, i) => (s, i)))
            {
                var current = StatText.Parse(socket.Display);
                if (socket.GemId is not null && current.Count == 0)
                {
                    continue;
                }

                options.AddRange(catalog
                    .Where(g => g.Stats.Count > 0 && g.Share >= RareShare && g.GemId != socket.GemId)
                    .Select(g => new Option(piece, index, socket.GemId is null ? SwapKind.FillSocket : SwapKind.Gem, socket.GemName, g.Name, g.IconUrl, current, g.Stats, g.Share)));
            }

            if (piece.Enchantments.FirstOrDefault() is { } enchant && StatText.Parse(enchant) is { Count: > 0 } enchantStats)
            {
                var label = StatText.ShortLabel(enchant);
                options.AddRange(enchants
                    .Where(e => e.Slot == piece.Slot && e.Stats.Count > 0 && e.Share >= RareShare && e.Label != label)
                    .Select(e => new Option(piece, 0, SwapKind.Enchant, label, e.Label, null, enchantStats, e.Stats, e.Share)));
            }
        }

        var swaps = new List<OptimizationSwap>();
        var used = new HashSet<(int ItemId, SwapKind Family, int Index)>();
        while (swaps.Count < MaxSwaps)
        {
            var before = Distance(percents, ranges);
            var best = options
                .Where(o => !used.Contains(o.Key))
                .Select(o => (Option: o, Gain: before - Distance(Apply(percents, o, rates), ranges)))
                // Une châsse vide se remplit même sans gain de répartition : c'est un gain d'indice gratuit.
                .Where(c => c.Gain >= MinImprovement || c.Option.Kind == SwapKind.FillSocket)
                .OrderByDescending(c => c.Option.Kind == SwapKind.FillSocket)
                .ThenByDescending(c => c.Gain)
                .ThenByDescending(c => c.Option.Share)
                .Select(c => c.Option)
                .FirstOrDefault();

            if (best is null)
            {
                break;
            }

            var after = Apply(percents, best, rates);
            var deltas = meta.Stats
                .Select(s => new StatDelta(s.Stat, after[s.Stat] - percents[s.Stat]))
                .Where(d => Math.Abs(d.Percent) >= 0.05)
                .ToList();
            swaps.Add(new OptimizationSwap(best.Kind, best.Piece.Slot, best.Piece.ItemId, best.Piece.Name, best.Index, best.From, best.To, best.IconUrl, deltas));
            used.Add(best.Key);
            percents = after;
        }

        var projection = meta.Stats
            .Select(s =>
            {
                var current = sheet.Get(s.Stat).Percent;
                var target = percents[s.Stat] < s.P25Percent ? s.P25Percent : percents[s.Stat] > s.P75Percent ? s.P75Percent : percents[s.Stat];
                return new StatProjection(s.Stat, current, percents[s.Stat], target - percents[s.Stat], rates[s.Stat] > 0 ? (target - percents[s.Stat]) / rates[s.Stat] : 0);
            })
            .ToList();

        return new SwapPlan(swaps, projection);
    }

    private static Dictionary<SecondaryStat, double> Apply(Dictionary<SecondaryStat, double> percents, Option option, Dictionary<SecondaryStat, double> rates)
    {
        var next = new Dictionary<SecondaryStat, double>(percents);
        foreach (var removed in option.Remove)
        {
            next[removed.Stat] -= removed.Value * rates[removed.Stat];
        }

        foreach (var added in option.Add)
        {
            next[added.Stat] += added.Value * rates[added.Stat];
        }

        return next;
    }

    /// <summary>Somme des carrés des écarts à la fourchette, mesurés en largeurs de fourchette. 0 quand tout est dedans.</summary>
    private static double Distance(Dictionary<SecondaryStat, double> percents, Dictionary<SecondaryStat, StatDistribution> ranges) =>
        percents.Sum(p =>
        {
            var range = ranges[p.Key];
            var outside = p.Value < range.P25Percent ? range.P25Percent - p.Value : p.Value > range.P75Percent ? p.Value - range.P75Percent : 0;
            var deviation = outside / Math.Max(2, range.P75Percent - range.P25Percent);
            return deviation * deviation;
        });

    /// <summary>
    /// % gagné par point d'indice, mesuré sur le top : les parts de maîtrise diffèrent par spécialisation, et l'API ne
    /// donne pas le coefficient. Sans dispersion exploitable, repli sur le rapport des médianes (qui inclut le % de base).
    /// </summary>
    private static double PercentPerRating(StatDistribution stat, IReadOnlyList<PlayerProfile> top)
    {
        var points = top.Select(p => p.Stats).OfType<StatSheet>().Select(s => s.Get(stat.Stat)).Where(v => v.Rating > 0).ToList();
        if (points.Count >= MinRegressionSample)
        {
            var meanRating = points.Average(v => v.Rating);
            var meanPercent = points.Average(v => v.Percent);
            var variance = points.Sum(v => (v.Rating - meanRating) * (v.Rating - meanRating));
            if (variance > 0)
            {
                var slope = points.Sum(v => (v.Rating - meanRating) * (v.Percent - meanPercent)) / variance;
                if (slope > 0)
                {
                    return slope;
                }
            }
        }

        return stat.MedianRating > 0 ? stat.MedianPercent / stat.MedianRating : 0;
    }

    private sealed record Option(
        GearPiece Piece, int Index, SwapKind Kind, string? From, string To, string? IconUrl,
        IReadOnlyList<ItemStat> Remove, IReadOnlyList<ItemStat> Add, double Share)
    {
        public (int, SwapKind, int) Key => (Piece.ItemId, Kind == SwapKind.Enchant ? SwapKind.Enchant : SwapKind.Gem, Index);
    }

    private sealed record SwapPlan(IReadOnlyList<OptimizationSwap> Swaps, IReadOnlyList<StatProjection> Projection);
}

public enum SwapKind
{
    FillSocket,
    Gem,
    Enchant
}

public sealed record GemEnchantReview(
    IReadOnlyList<StatBreakdown> Breakdown,
    IReadOnlyList<SocketReview> Sockets,
    IReadOnlyList<EnchantReview> Enchants,
    IReadOnlyList<GemUsage> TopGems,
    IReadOnlyList<OptimizationSwap> Swaps,
    IReadOnlyList<StatProjection> Projection,
    IReadOnlyList<GearSlot> MissingSockets,
    bool TopHasDetails);

/// <summary>Indice d'une caractéristique par origine : pièces, gemmes, enchantements ; les « Top » sont des médianes.</summary>
public sealed record StatBreakdown(SecondaryStat Stat, int Items, int Gems, int Enchants, double TopItems, double TopGems, double TopEnchants);

public sealed record SocketReview(
    GearSlot Slot, int ItemId, string ItemName, int? GemId, string? GemName, string? IconUrl,
    IReadOnlyList<ItemStat> Stats, double TopShare, Verdict Verdict, OptimizationSwap? Swap);

public sealed record EnchantReview(
    GearSlot Slot, int ItemId, string ItemName, string? Label, IReadOnlyList<ItemStat> Stats,
    double TopShare, string? TopChoice, double TopChoiceShare, Verdict Verdict, OptimizationSwap? Swap);

/// <summary>Part des gemmes posées dans le top.</summary>
public sealed record GemUsage(int GemId, string Name, string? IconUrl, double Share, IReadOnlyList<ItemStat> Stats);

/// <summary>Part des enchantements posés sur cet emplacement dans le top.</summary>
public sealed record EnchantUsage(GearSlot Slot, string Label, double Share, IReadOnlyList<ItemStat> Stats);

/// <summary>Un changement proposé et son effet estimé, en points de pourcentage de la feuille de personnage.</summary>
public sealed record OptimizationSwap(
    SwapKind Kind, GearSlot Slot, int ItemId, string ItemName, int SocketIndex, string? From, string To, string? ToIconUrl, IReadOnlyList<StatDelta> Deltas);

public sealed record StatDelta(SecondaryStat Stat, double Percent);

/// <summary>
/// Une caractéristique avant et après les changements proposés. <see cref="RemainingPercent"/> et <see cref="RemainingRating"/> :
/// ce qui manque (positif) ou dépasse (négatif) ensuite par rapport à la fourchette du top : à régler par l'équipement.
/// </summary>
public sealed record StatProjection(SecondaryStat Stat, double Current, double Projected, double RemainingPercent, double RemainingRating);
