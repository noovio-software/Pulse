using Microsoft.Extensions.Options;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Enums;
using Noovio.Pulse.Domain.Roles;

namespace Noovio.Pulse.Application.Meta;

/// <summary>
/// Phase 3 : transforme les profils d'une spécialisation en snapshot méta.
/// Calcul pur (aucune E/S), exécuté par le worker ; le site ne lit que le résultat.
/// Toutes les parts sont rapportées à la taille de l'échantillon : un objet porté par 40 joueurs sur 100 vaut 0,4.
/// </summary>
public sealed class MetaAggregatorService(IOptions<MetaOptions> options, PulseRole role)
{
    private readonly MetaOptions _options = options.Value;

    public SpecMetaSnapshot Aggregate(WowSpec spec, string season, IReadOnlyList<PlayerProfile> profiles, DateTimeOffset computedAtUtc)
    {
        var n = profiles.Count;
        var slots = Enum.GetValues<GearSlot>()
            .Select(slot => new SlotUsage(slot, TopItems(profiles, p => p.Gear.Where(g => g.Slot == slot), _options.TopItemsPerSlot)))
            .Where(s => s.Items.Count > 0)
            .ToList();

        return new SpecMetaSnapshot(
            spec,
            season,
            computedAtUtc,
            n,
            Median(profiles.Where(p => p.ItemLevel > 0).Select(p => p.ItemLevel)),
            (int)Math.Round(Median(profiles.Select(p => (double)p.BestKeyLevel))),
            Stats(profiles),
            slots,
            TopItems(profiles, p => Trinkets(p, TrinketOrientation.Role), _options.TopTrinketsPerOrientation),
            TopItems(profiles, p => Trinkets(p, TrinketOrientation.Damage), _options.TopTrinketsPerOrientation),
            Embellishments(profiles),
            TalentBuilds(profiles),
            HeroTrees(profiles),
            TalentPicks(profiles),
            profiles.OrderBy(p => p.Rank)
                .Select(p => new TopPlayer(p.Rank, p.Name, p.Region, p.RealmSlug, p.BestRunScore, p.BestKeyLevel, p.ItemLevel, p.ProfileUrl))
                .ToList());
    }

    public TrinketOrientation Orientation(GearPiece trinket)
    {
        if (_options.RoleTrinketIds.Contains(trinket.ItemId))
        {
            return TrinketOrientation.Role;
        }

        if (_options.DamageTrinketIds.Contains(trinket.ItemId))
        {
            return TrinketOrientation.Damage;
        }

        var servesRole = trinket.Effects.Any(e => e.Description is { } d &&
            role.TrinketKeywords.Any(k => d.Contains(k, StringComparison.OrdinalIgnoreCase)));
        return servesRole ? TrinketOrientation.Role : TrinketOrientation.Damage;
    }

    /// <summary>Objets dont le snapshot affiche l'icône.</summary>
    public static IEnumerable<int> ItemIds(SpecMetaSnapshot snapshot) =>
        snapshot.Slots.SelectMany(s => s.Items)
            .Concat(snapshot.RoleTrinkets)
            .Concat(snapshot.DamageTrinkets)
            .Select(i => i.ItemId)
            .Concat(snapshot.Embellishments.SelectMany(c => c.Parts).Select(p => p.ItemId))
            .Distinct();

    /// <summary>Sorts dont le snapshot affiche l'icône (embellissements ; l'arbre de talents arrive avec les siennes).</summary>
    public static IEnumerable<int> SpellIds(SpecMetaSnapshot snapshot) =>
        snapshot.Embellishments.SelectMany(c => c.Parts).Where(p => p.SpellId > 0).Select(p => p.SpellId).Distinct();

    public static SpecMetaSnapshot WithIcons(SpecMetaSnapshot snapshot, IReadOnlyDictionary<int, string> itemIcons, IReadOnlyDictionary<int, string> spellIcons)
    {
        List<ItemStatSnapshot> Apply(IEnumerable<ItemStatSnapshot> items) =>
            items.Select(i => itemIcons.TryGetValue(i.ItemId, out var url) ? i with { IconUrl = url } : i).ToList();

        // L'API n'a pas de média pour la plupart des sorts d'embellissement : l'icône de la pièce prend le relais.
        string? PartIcon(EmbellishmentPart p) =>
            spellIcons.GetValueOrDefault(p.SpellId) ?? itemIcons.GetValueOrDefault(p.ItemId);

        return snapshot with
        {
            Slots = snapshot.Slots.Select(s => s with { Items = Apply(s.Items) }).ToList(),
            RoleTrinkets = Apply(snapshot.RoleTrinkets),
            DamageTrinkets = Apply(snapshot.DamageTrinkets),
            Embellishments = snapshot.Embellishments
                .Select(c => c with { Parts = c.Parts.Select(p => p with { IconUrl = PartIcon(p) }).ToList() })
                .ToList()
        };
    }

    /// <summary>
    /// Reporte les taux de sélection du snapshot sur l'arbre. Les arbres héroïques sont triés par popularité,
    /// et ceux que personne ne joue dans le top sont retirés.
    /// </summary>
    public static SpecMetaSnapshot WithTalentTree(SpecMetaSnapshot snapshot, TalentTreeLayout? layout)
    {
        if (layout is null)
        {
            return snapshot;
        }

        var picks = snapshot.Talents.ToDictionary(t => (t.Tree, t.TalentId), t => t.Share);
        var heroShares = snapshot.HeroTrees.ToDictionary(h => h.Name, h => h.Share);

        var sections = layout.Sections
            .Select(section => section with
            {
                Share = section.Tree == TalentTree.Hero ? heroShares.GetValueOrDefault(section.Name) : 1,
                Nodes = section.Nodes
                    .Select(n => n with { Choices = n.Choices.Select(c => c with { Share = picks.GetValueOrDefault((section.Tree, c.TalentId)) }).ToList() })
                    .ToList()
            })
            .Where(s => s.Tree != TalentTree.Hero || s.Share > 0)
            .OrderBy(s => s.Tree)
            .ThenByDescending(s => s.Share)
            .ToList();

        return snapshot with { TalentTree = new TalentTreeLayout(sections) };
    }

    private IEnumerable<GearPiece> Trinkets(PlayerProfile profile, TrinketOrientation orientation) =>
        profile.Gear.Where(g => g.Slot == GearSlot.Trinket && Orientation(g) == orientation);

    private static List<ItemStatSnapshot> TopItems(IReadOnlyList<PlayerProfile> profiles, Func<PlayerProfile, IEnumerable<GearPiece>> select, int take)
    {
        var n = profiles.Count;
        return profiles
            // Un joueur ne compte qu'une fois par objet, même s'il porte deux exemplaires (anneaux, armes).
            .SelectMany(p => select(p).DistinctBy(g => g.ItemId))
            .GroupBy(g => g.ItemId)
            .Select(g => new ItemStatSnapshot(
                g.Key,
                g.First().Name,
                null,
                g.Count(),
                Share(g.Count(), n),
                (int)Math.Round(Median(g.Select(x => (double)x.ItemLevel)))))
            .OrderByDescending(i => i.Count)
            .ThenBy(i => i.Name, StringComparer.Ordinal)
            .Take(take)
            .ToList();
    }

    /// <summary>
    /// Médianes plutôt que moyennes : un joueur à 60 % de hâte ne tire pas la répartition.
    /// Les parts médianes sont renormalisées pour que les quatre jauges totalisent 100 %.
    /// </summary>
    private static List<StatDistribution> Stats(IReadOnlyList<PlayerProfile> profiles)
    {
        var sheets = profiles.Select(p => p.Stats).OfType<StatSheet>().Where(s => s.TotalRating > 0).ToList();
        if (sheets.Count == 0)
        {
            return [];
        }

        var raw = Enum.GetValues<SecondaryStat>()
            .Select(stat =>
            {
                var percents = sheets.Select(s => s.Get(stat).Percent).ToList();
                return (
                    Stat: stat,
                    Rating: Median(sheets.Select(s => s.Get(stat).Rating)),
                    Percent: Median(percents),
                    Share: Median(sheets.Select(s => s.Get(stat).Rating / s.TotalRating)),
                    P25: Quantile(percents, 0.25),
                    P75: Quantile(percents, 0.75));
            })
            .ToList();

        var totalShare = raw.Sum(r => r.Share);
        return raw
            .Select(r => new StatDistribution(r.Stat, r.Rating, r.Percent, totalShare > 0 ? r.Share / totalShare : 0, r.P25, r.P75))
            .OrderByDescending(s => s.MedianShare)
            .ToList();
    }

    private List<EmbellishmentCombo> Embellishments(IReadOnlyList<PlayerProfile> profiles) =>
        profiles
            .Select(p => p.Gear
                .Where(g => g.IsEmbellished)
                // L'embellissement se lit dans l'effet de l'objet ; à défaut, on garde la pièce craftée.
                .Select(g => g.Effects.FirstOrDefault() is { } effect
                    ? new EmbellishmentPart(effect.Name, effect.SpellId, g.ItemId, null)
                    : new EmbellishmentPart(g.Name, 0, g.ItemId, null))
                // Deux pièces au même embellissement : une seule entrée « ×2 ».
                .GroupBy(e => e.Name)
                .Select(same => same.First() with { Count = same.Count() })
                .OrderBy(e => e.Name, StringComparer.Ordinal)
                .ToList())
            .Where(parts => parts.Count > 0)
            .GroupBy(parts => string.Join(" + ", parts.Select(e => $"{e.Name}×{e.Count}")))
            .Select(g => new EmbellishmentCombo(g.First(), g.Count(), Share(g.Count(), profiles.Count)))
            .OrderByDescending(c => c.Count)
            .Take(_options.TopEmbellishmentCombos)
            .ToList();

    private List<TalentBuild> TalentBuilds(IReadOnlyList<PlayerProfile> profiles) =>
        profiles
            .Where(p => !string.IsNullOrEmpty(p.LoadoutCode))
            .GroupBy(p => p.LoadoutCode!)
            .Select(g => new TalentBuild(
                g.Key,
                g.Where(p => p.HeroTree is not null).GroupBy(p => p.HeroTree).OrderByDescending(h => h.Count()).FirstOrDefault()?.Key,
                g.Count(),
                Share(g.Count(), profiles.Count)))
            .OrderByDescending(b => b.Count)
            .Take(_options.TopTalentBuilds)
            .ToList();

    private static List<HeroTreeUsage> HeroTrees(IReadOnlyList<PlayerProfile> profiles) =>
        profiles
            .Where(p => p.HeroTree is not null)
            .GroupBy(p => p.HeroTree!)
            .Select(g => new HeroTreeUsage(g.Key, g.Count(), Share(g.Count(), profiles.Count)))
            .OrderByDescending(h => h.Count)
            .ToList();

    private static List<TalentPickRate> TalentPicks(IReadOnlyList<PlayerProfile> profiles)
    {
        var withTalents = profiles.Where(p => p.Talents.Count > 0).ToList();
        return withTalents
            .SelectMany(p => p.Talents.DistinctBy(t => (t.Tree, t.TalentId)))
            .GroupBy(t => (t.Tree, t.TalentId))
            .Select(g => new TalentPickRate(
                g.Key.Tree,
                g.Key.TalentId,
                g.First().SpellId,
                g.First().Name,
                g.Average(t => t.Rank),
                Share(g.Count(), withTalents.Count)))
            .OrderBy(t => t.Tree)
            .ThenByDescending(t => t.Share)
            .ThenBy(t => t.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static double Share(int count, int total) => total == 0 ? 0 : (double)count / total;

    public static double Median(IEnumerable<double> values) => Quantile(values, 0.5);

    /// <summary>Quantile par interpolation linéaire entre les deux valeurs encadrantes (0 si vide).</summary>
    public static double Quantile(IEnumerable<double> values, double q)
    {
        var sorted = values.Order().ToArray();
        if (sorted.Length == 0)
        {
            return 0;
        }

        var position = (sorted.Length - 1) * q;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
    }
}
