using System.Text.RegularExpressions;
using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Domain.Entities;
using Noovio.FrontPulse.Domain.Enums;

namespace Noovio.FrontPulse.Application.Analysis;

/// <summary>
/// Bijoux, sans E/S : quelle paire porter (celle du top), quoi remplacer et où le trouver, comment jouer chaque bijou.
/// En complément, les coups de boss qui visent le tank, lus dans les puces des sections « Tank » du Journal d'aventure :
/// ces sections existent sur toutes les rencontres, seule une attaque décrite dans une puce permet de les distinguer.
/// </summary>
public static partial class TrinketPlanner
{
    /// <summary>Un bijou porté par au moins cette part du top se garde.</summary>
    private const double KeepShare = 0.1;

    /// <summary>Un bijou n'est visé que s'il est au moins aussi répandu dans le top.</summary>
    private const double TargetShare = 0.15;

    private const double MinPairShare = 0.05;
    private const int MaxPairs = 3;

    /// <summary>Score à partir duquel un coup est retenu : dégâts physiques sur la cible, ou annoncés comme lourds.</summary>
    private const int HitThreshold = 2;

    private static readonly string[] AreaWords = ["tous les personnages-joueurs", "tous les joueurs", "répartis", "all players"];
    private static readonly string[] MeleeWords = ["dégâts physiques", "dégâts de mêlée", "physical damage", "melee damage"];
    private static readonly string[] TargetWords = ["cible actuelle", "sa cible", "current target", "their target", "its target"];
    private static readonly string[] HeavyWords = ["importants", "lourds", "croissants", "vulnérabilité", "subis", "heavy", "massive", "increasing", "vulnerab"];

    public static TrinketPlan Build(
        IReadOnlyList<GearPiece> gear,
        SpecMetaSnapshot meta,
        IReadOnlyList<TankProfile> top,
        EncounterJournal? journal,
        Func<GearPiece, TrinketOrientation>? orientation,
        IReadOnlyDictionary<int, string> icons)
    {
        var n = Math.Max(1, top.Count);
        var topPieces = top.SelectMany(p => p.Gear).Where(g => g.Slot == GearSlot.Trinket).ToList();
        var popular = meta.Slots.FirstOrDefault(s => s.Slot == GearSlot.Trinket)?.Items ?? [];
        var sources = (journal?.Instances ?? [])
            .OrderBy(i => i.Kind)
            .SelectMany(i => i.Bosses.SelectMany(b => b.ItemIds.Select(id => (Id: id, Instance: i, Boss: b.Name))))
            .GroupBy(s => s.Id)
            .ToDictionary(g => g.Key, g => g.First());
        var worn = gear.Where(g => g.Slot == GearSlot.Trinket).ToList();
        var targets = Targets(gear, top, meta);

        TrinketUsage Usage(GearPiece piece) => new(
            piece.Effects.Any(e => IsOnUse(e.Description)),
            orientation?.Invoke(piece) ?? TrinketOrientation.Offensive,
            Summary(piece),
            Recharge(piece));

        TrinketRef Reference(ItemStatSnapshot item)
        {
            var source = sources.GetValueOrDefault(item.ItemId);
            return new TrinketRef(
                item.ItemId, item.Name, item.IconUrl ?? icons.GetValueOrDefault(item.ItemId), item.Share, item.MedianItemLevel,
                source.Instance?.Name, source.Instance?.Kind, source.Boss,
                topPieces.FirstOrDefault(p => p.ItemId == item.ItemId) is { } piece ? Usage(piece) : null);
        }

        var trinkets = worn
            .Select(piece =>
            {
                var usage = popular.FirstOrDefault(p => p.ItemId == piece.ItemId);
                var replacement = targets.TryGetValue(piece.ItemId, out var targetId) ? popular.First(p => p.ItemId == targetId) : null;
                return new TrinketAdvice(
                    piece.ItemId, piece.Name, icons.GetValueOrDefault(piece.ItemId), piece.ItemLevel,
                    usage?.MedianItemLevel, (double)top.Count(p => p.Gear.Any(g => g.ItemId == piece.ItemId)) / n,
                    Usage(piece), replacement is null ? null : Reference(replacement));
            })
            .ToList();

        var wornIds = worn.Select(g => g.ItemId).ToHashSet();
        var pairs = Pairs(top)
            .Where(p => p.Share >= MinPairShare)
            .Take(MaxPairs)
            .Select(p => new TrinketPair(
                p.Items.Select(id => popular.FirstOrDefault(i => i.ItemId == id)).OfType<ItemStatSnapshot>().Select(Reference).ToList(),
                p.Share,
                wornIds.SetEquals(p.Items)))
            .Where(p => p.Items.Count == 2)
            .ToList();

        var hits = (journal?.Instances ?? [])
            .OrderBy(i => i.Kind)
            .Select(i => new InstanceTankHits(i.Name, i.Kind, i.Bosses.Select(Hit).OfType<BossHit>().OrderByDescending(h => h.Score).ToList()))
            .Where(i => i.Bosses.Count > 0)
            .ToList();

        return new TrinketPlan(pairs, trinkets, hits, journal is not null);
    }

    /// <summary>
    /// Bijou porté à remplacer → bijou visé. Un bijou répandu se garde, et son remplaçant est d'abord cherché dans les
    /// paires du top qui contiennent celui qu'on garde. Partagé avec la roadmap pour que les deux sections disent la même chose.
    /// </summary>
    public static IReadOnlyDictionary<int, int> Targets(IReadOnlyList<GearPiece> gear, IReadOnlyList<TankProfile> top, SpecMetaSnapshot meta)
    {
        var n = Math.Max(1, top.Count);
        var popular = meta.Slots.FirstOrDefault(s => s.Slot == GearSlot.Trinket)?.Items ?? [];
        var worn = gear.Where(g => g.Slot == GearSlot.Trinket).ToList();
        double Share(int id) => (double)top.Count(p => p.Gear.Any(g => g.ItemId == id)) / n;

        var kept = worn.Where(g => Share(g.ItemId) >= KeepShare).Select(g => g.ItemId).ToHashSet();
        var fromPairs = Pairs(top).Where(p => kept.IsSubsetOf(p.Items)).SelectMany(p => p.Items);
        var candidates = fromPairs
            .Concat(popular.Select(p => p.ItemId))
            .Distinct()
            .Where(id => worn.All(g => g.ItemId != id) && popular.Any(p => p.ItemId == id && p.Share >= TargetShare))
            .ToList();

        var targets = new Dictionary<int, int>();
        foreach (var piece in worn.Where(g => !kept.Contains(g.ItemId)).OrderBy(g => g.ItemLevel))
        {
            var target = candidates.Where(c => !targets.ContainsValue(c)).Cast<int?>().FirstOrDefault();
            if (target is { } id && Share(id) > Share(piece.ItemId))
            {
                targets[piece.ItemId] = id;
            }
        }

        return targets;
    }

    private static IEnumerable<(IReadOnlyList<int> Items, double Share)> Pairs(IReadOnlyList<TankProfile> top)
    {
        var n = Math.Max(1, top.Count);
        return top
            .Select(p => p.Gear.Where(g => g.Slot == GearSlot.Trinket).Select(g => g.ItemId).Order().ToList())
            .Where(ids => ids.Count == 2)
            .GroupBy(ids => (ids[0], ids[1]))
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => ((IReadOnlyList<int>)g.First(), (double)g.Count() / n));
    }

    /// <summary>Le coup le plus dur d'un boss d'après les puces de ses sections « Tank », ou null si aucune n'en décrit.</summary>
    public static BossHit? Hit(JournalBoss boss) =>
        boss.Abilities
            .Where(a => a.TankHeading)
            .SelectMany(a => a.Text.Split("$bullet;"))
            .Select(bullet => (Text: bullet, Score: Score(bullet)))
            .Where(b => b.Score >= HitThreshold)
            .OrderByDescending(b => b.Score)
            .Select(b => new BossHit(boss.Name, Brackets().Match(b.Text) is { Success: true } m ? m.Groups[1].Value : null, CleanBullet(b.Text), b.Score))
            .FirstOrDefault();

    private static int Score(string bullet)
    {
        if (Has(bullet, AreaWords))
        {
            return 0;
        }

        var melee = Has(bullet, MeleeWords);
        var target = Has(bullet, TargetWords);
        if (!melee && !target)
        {
            return 0;
        }

        return (melee ? 1 : 0) + (target ? 1 : 0) + (Has(bullet, HeavyWords) ? 2 : 0);
    }

    private static bool Has(string text, string[] words) => words.Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));

    private static string CleanBullet(string bullet) => bullet.Replace("[", "").Replace("]", "").Trim();

    /// <summary>Blizzard préfixe les effets à déclencher : « Utiliser : » (fr_FR), « Use: » (en_US).</summary>
    private static bool IsOnUse(string? description) =>
        description is not null
        && (description.TrimStart().StartsWith("Utiliser", StringComparison.OrdinalIgnoreCase)
            || description.TrimStart().StartsWith("Use:", StringComparison.OrdinalIgnoreCase));

    /// <summary>Premier paragraphe de l'effet, sans le préfixe « Utiliser : » / « Équipé : » ni la recharge.</summary>
    private static string? Summary(GearPiece piece)
    {
        var description = piece.Effects.Select(e => e.Description).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));
        if (description is null)
        {
            return null;
        }

        var first = description.Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries)[0];
        return RechargeText().Replace(EffectPrefix().Replace(first, ""), "").Trim();
    }

    private static string? Recharge(GearPiece piece) =>
        piece.Effects.Select(e => RechargeText().Match(e.Description ?? "")).FirstOrDefault(m => m.Success)?.Groups[1].Value;

    [GeneratedRegex(@"\[([^\]]+)\]")]
    private static partial Regex Brackets();

    [GeneratedRegex(@"^\s*(Utiliser|Équipé|Use|Equip)\s*:\s*", RegexOptions.IgnoreCase)]
    private static partial Regex EffectPrefix();

    [GeneratedRegex(@"\(([^()]*(?:recharge|cooldown)[^()]*)\)", RegexOptions.IgnoreCase)]
    private static partial Regex RechargeText();
}

/// <summary>
/// <see cref="Pairs"/> : paires les plus portées du top ; <see cref="Trinkets"/> : les bijoux du joueur et leur verdict ;
/// <see cref="TankHits"/> : coups de boss visant le tank, par instance.
/// </summary>
public sealed record TrinketPlan(IReadOnlyList<TrinketPair> Pairs, IReadOnlyList<TrinketAdvice> Trinkets, IReadOnlyList<InstanceTankHits> TankHits, bool HasJournal);

public sealed record TrinketPair(IReadOnlyList<TrinketRef> Items, double Share, bool IsWorn);

/// <summary><see cref="ReplaceWith"/> : null si le bijou se garde. <see cref="TopItemLevel"/> : médiane du top sur ce bijou, s'il en porte.</summary>
public sealed record TrinketAdvice(
    int ItemId, string Name, string? IconUrl, int ItemLevel, int? TopItemLevel, double TopShare, TrinketUsage Usage, TrinketRef? ReplaceWith);

/// <summary>Un bijou du top et où il tombe (null si le journal ne le liste pas).</summary>
public sealed record TrinketRef(
    int ItemId, string Name, string? IconUrl, double TopShare, int ItemLevel, string? Instance, InstanceKind? Kind, string? Boss, TrinketUsage? Usage);

/// <summary><see cref="Recharge"/> : tel que dans le texte Blizzard (« 1 min 30 s de recharge »).</summary>
public sealed record TrinketUsage(bool OnUse, TrinketOrientation Orientation, string? Effect, string? Recharge);

public sealed record InstanceTankHits(string Instance, InstanceKind Kind, IReadOnlyList<BossHit> Bosses);

/// <summary><see cref="Text"/> : la puce du journal, citée telle quelle ; <see cref="Ability"/> : la technique qu'elle nomme.</summary>
public sealed record BossHit(string Boss, string? Ability, string Text, int Score);
