using Noovio.Pulse.Application.Import;

namespace Noovio.Pulse.Application.Analysis;

/// <summary>Niveau d'objet d'une pièce améliorée au maximum de sa piste, « Héros 6/6 » ; estimé quand le jeu ne l'a pas donné.</summary>
public readonly record struct UpgradePotential(int Level, string? Label, bool Estimated);

/// <summary>
/// Niveau maximum de chaque pièce, à partir de sa piste d'amélioration (« Héros 2/6 »). Le jeu donne ce maximum pour les
/// pièces portées ou en sac ; pour les autres (coffre), il est déduit des pièces de la même piste : même maximum, ou écart
/// par rang observé entre deux rangs ; à défaut, <see cref="DefaultStep"/> niveaux par rang.
/// </summary>
public sealed class UpgradeScale
{
    /// <summary>Écart habituel entre deux rangs d'une piste, faute de mieux.</summary>
    public const int DefaultStep = 3;

    private readonly Dictionary<string, int> maxByTrack;
    private readonly Dictionary<string, double> stepByTrack;

    private UpgradeScale(Dictionary<string, int> maxByTrack, Dictionary<string, double> stepByTrack)
    {
        this.maxByTrack = maxByTrack;
        this.stepByTrack = stepByTrack;
    }

    public static UpgradeScale From(IEnumerable<ImportedGear> items)
    {
        var tracked = items.Where(i => i.Upgrade is { MaxRank: > 1, Track.Length: > 0 }).ToList();

        var maxByTrack = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in tracked)
        {
            var known = item.Upgrade!.MaxIlvl is > 0 ? item.Upgrade.MaxIlvl.Value
                : item.Upgrade.Rank >= item.Upgrade.MaxRank ? item.Ilvl
                : 0;
            if (known > 0 && (!maxByTrack.TryGetValue(item.Upgrade.Track!, out var seen) || known > seen))
            {
                maxByTrack[item.Upgrade.Track!] = known;
            }
        }

        var stepByTrack = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in tracked.GroupBy(i => i.Upgrade!.Track!, StringComparer.OrdinalIgnoreCase))
        {
            var byRank = group.GroupBy(i => i.Upgrade!.Rank).Select(g => (Rank: g.Key, Ilvl: g.Min(i => i.Ilvl))).OrderBy(x => x.Rank).ToList();
            if (byRank.Count >= 2 && byRank[^1].Rank > byRank[0].Rank && byRank[^1].Ilvl > byRank[0].Ilvl)
            {
                stepByTrack[group.Key] = (double)(byRank[^1].Ilvl - byRank[0].Ilvl) / (byRank[^1].Rank - byRank[0].Rank);
            }
        }

        return new UpgradeScale(maxByTrack, stepByTrack);
    }

    public UpgradePotential Max(ImportedGear item)
    {
        if (item.Upgrade is not { MaxRank: > 1 } upgrade)
        {
            return new UpgradePotential(item.Ilvl, null, false); // artisanat, légendaire… : niveau fixe
        }

        var label = $"{upgrade.Track} {upgrade.MaxRank}/{upgrade.MaxRank}".Trim();
        if (upgrade.Rank >= upgrade.MaxRank)
        {
            return new UpgradePotential(item.Ilvl, label, false);
        }

        if (upgrade.MaxIlvl is > 0)
        {
            return new UpgradePotential(Math.Max(item.Ilvl, upgrade.MaxIlvl.Value), label, false);
        }

        if (upgrade.Track is { Length: > 0 } track && maxByTrack.TryGetValue(track, out var max))
        {
            return new UpgradePotential(Math.Max(item.Ilvl, max), label, false);
        }

        var step = upgrade.Track is { Length: > 0 } t && stepByTrack.TryGetValue(t, out var observed) ? observed : DefaultStep;
        return new UpgradePotential(item.Ilvl + (int)Math.Round((upgrade.MaxRank - upgrade.Rank) * step), label, true);
    }
}
