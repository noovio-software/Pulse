using Noovio.Pulse.Application.Import;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Enums;
using Noovio.Pulse.Domain.Roles;

namespace Noovio.Pulse.Application.Analysis;

/// <summary>
/// Sacs et Grande Chambre forte, connus seulement par l'addon : pièces qui attendent dans les sacs, objets à choisir dans
/// le coffre (comparés à la pièce portée et à la méta), consommables prêts pour une clé.
/// </summary>
public sealed record InventoryReview(
    Verdict Verdict,
    string Summary,
    IReadOnlyList<ItemOption> BagUpgrades,
    int BagGearCount,
    VaultReview? Vault,
    IReadOnlyList<ConsumableCheck> Consumables);

/// <summary>
/// Une pièce candidate (sac ou coffre) face à celle portée sur l'emplacement. <see cref="Recommended"/> : à équiper ou à
/// choisir ; <see cref="Reason"/> : pourquoi, en une phrase.
/// </summary>
public sealed record ItemOption(
    GearSlot Slot,
    int ItemId,
    string Name,
    string? IconUrl,
    int ItemLevel,
    int MaxItemLevel,
    string? Upgrade,
    bool MaxEstimated,
    IReadOnlyList<int> Bonus,
    int EquippedItemLevel,
    int EquippedMaxItemLevel,
    string? EquippedName,
    double TopShare,
    bool IsSetPiece,
    bool Recommended,
    string Reason)
{
    /// <summary>Écart une fois les deux pièces améliorées au maximum de leur piste : c'est lui qui décide.</summary>
    public int Gain => MaxItemLevel - EquippedMaxItemLevel;
}

/// <summary>
/// <see cref="RewardsReady"/> : coffre à ouvrir, <see cref="Choices"/> = objets proposés ; sinon la progression de la semaine.
/// </summary>
public sealed record VaultReview(bool RewardsReady, IReadOnlyList<VaultTrack> Tracks, IReadOnlyList<ItemOption> Choices, string Advice);

public sealed record VaultTrack(string Label, string Unit, IReadOnlyList<VaultSlotView> Slots);

public sealed record VaultSlotView(int Threshold, int Progress, int? ItemLevel)
{
    public bool Unlocked => Progress >= Threshold;
}

public enum ConsumableKind
{
    Flask,
    CombatPotion,
    HealthPotion,
    ManaPotion,
    Food,
    WeaponBuff,
    AugmentRune
}

/// <summary>Un type de consommable attendu en clé : le meilleur trouvé dans les sacs (le plus nombreux), ou rien.</summary>
public sealed record ConsumableCheck(
    ConsumableKind Kind,
    string Label,
    Verdict Verdict,
    int ItemId,
    string? ItemName,
    string? IconUrl,
    int Count,
    int? Quality,
    string Advice);

public static class InventoryAnalyzer
{
    private const int RogueClass = 4;
    private const int ShamanClass = 7;

    /// <summary>Écart d'ilvl au-delà duquel une pièce plus haute vaut la perte d'un bonus de set ou d'un embellissement.</summary>
    private const int BigGain = 13;

    /// <summary>Part du top à partir de laquelle un bijou ou une arme est un vrai choix de la méta.</summary>
    private const double MetaShare = 0.15;

    private const int LowPotionStock = 5;

    public static IEnumerable<int> ItemIds(CharacterImport import) =>
        (import.Bags?.Gear ?? []).Select(g => g.Id)
        .Concat((import.Bags?.Consumables ?? []).Select(c => c.Id))
        .Concat((import.Vault?.Activities ?? []).SelectMany(a => a.Items ?? []).Select(i => i.Id))
        .Where(id => id > 0)
        .Distinct();

    public static InventoryReview? Analyze(CharacterImport import, SpecMetaSnapshot meta, PulseRole role, IReadOnlyDictionary<int, string> icons)
    {
        if (import.Bags is null && import.Vault is null)
        {
            return null; // export d'une version de l'addon sans sacs ni coffre
        }

        var equipped = (import.Gear ?? []).Where(g => GearRules.SlotOf(g.Slot) is not null).ToList();
        var bagGear = (import.Bags?.Gear ?? []).Where(g => g.Usable && GearRules.SlotOf(g.Slot) is not null).ToList();
        var scale = UpgradeScale.From(equipped.Concat(import.Bags?.Gear ?? []).Concat((import.Vault?.Activities ?? []).SelectMany(a => a.Items ?? [])));
        var bagOptions = bagGear
            .Select(g => Evaluate(g, equipped, meta, icons, scale))
            .Where(o => o.Recommended)
            .OrderByDescending(o => o.TopShare >= MetaShare)
            .ThenByDescending(o => o.Gain)
            .ToList();

        var vault = import.Vault is null ? null : Vault(import.Vault, equipped, meta, icons, scale);
        var consumables = import.Bags is null ? [] : Consumables(import.Bags.Consumables ?? [], role, import.Character?.Class ?? 0, icons);

        var problems = consumables.Count(c => c.Verdict == Verdict.Problem);
        var improvements = consumables.Count(c => c.Verdict == Verdict.Improve) + bagOptions.Count + (vault?.RewardsReady == true ? 1 : 0);
        var verdict = problems > 0 ? Verdict.Problem : improvements > 0 ? Verdict.Improve : Verdict.Good;

        var parts = new List<string>();
        if (bagOptions.Count > 0)
        {
            parts.Add($"{bagOptions.Count} amélioration{Plural(bagOptions.Count)} dans les sacs");
        }

        if (vault?.RewardsReady == true)
        {
            parts.Add("coffre à ouvrir");
        }

        var missing = consumables.Where(c => c.Count == 0 && c.Verdict != Verdict.Good).ToList();
        if (missing.Count > 0)
        {
            parts.Add($"{missing.Count} consommable{Plural(missing.Count)} manquant{Plural(missing.Count)}");
        }

        return new InventoryReview(
            verdict,
            parts.Count == 0 ? "Sacs prêts pour la clé" : string.Join(" · ", parts),
            bagOptions,
            bagGear.Count,
            vault,
            consumables);
    }

    /// <summary>
    /// Une pièce face à celle portée sur son emplacement (la plus basse des deux pour les anneaux et bijoux). Bijoux et armes
    /// valent par leur effet : on suit le top. Ailleurs, le niveau d'objet décide, sauf bonus de set ou embellissement perdus.
    /// </summary>
    private static ItemOption Evaluate(ImportedGear candidate, IReadOnlyList<ImportedGear> equipped, SpecMetaSnapshot meta, IReadOnlyDictionary<int, string> icons, UpgradeScale scale)
    {
        var slot = GearRules.SlotOf(candidate.Slot)!.Value;
        var popular = meta.Slots.FirstOrDefault(s => s.Slot == slot)?.Items ?? [];
        double ShareOf(int itemId) => popular.FirstOrDefault(i => i.ItemId == itemId)?.Share ?? 0;

        var current = equipped
            .Where(g => GearRules.SlotOf(g.Slot) == slot)
            .OrderBy(g => slot is GearSlot.Trinket ? ShareOf(g.Id) : 0)
            .ThenBy(g => scale.Max(g).Level)
            .FirstOrDefault();
        var share = ShareOf(candidate.Id);
        var isSet = candidate.SetId is > 0;
        var name = string.IsNullOrWhiteSpace(candidate.Name) ? $"#{candidate.Id}" : candidate.Name;
        var max = scale.Max(candidate);
        var currentMax = current is null ? default : scale.Max(current);
        ItemOption Option(bool recommended, string reason) => new(
            slot, candidate.Id, name, icons.GetValueOrDefault(candidate.Id), candidate.Ilvl, max.Level, max.Label, max.Estimated, candidate.Bonus ?? [],
            current?.Ilvl ?? 0, currentMax.Level, current?.Name, share, isSet, recommended, reason);

        if (current is null)
        {
            return Option(true, "Emplacement vide : à équiper.");
        }

        if (current.Id == candidate.Id && max.Level <= currentMax.Level)
        {
            return Option(false, "Même pièce que celle portée.");
        }

        // Comparées au maximum de leur piste : une pièce Héros 1/6 bat une Champion 6/6 qui ne montera plus.
        var gain = max.Level - currentMax.Level;
        var atMax = max.Label is null ? "" : $" au max ({max.Label})";
        var currentShare = ShareOf(current.Id);
        if (slot is GearSlot.Trinket or GearSlot.MainHand or GearSlot.OffHand)
        {
            // Un net écart de popularité vaut quelques niveaux d'objet : l'effet compte plus que l'ilvl.
            if (share >= MetaShare && share > currentShare + 0.05 && gain >= (share - currentShare >= 0.25 ? -BigGain : -6))
            {
                return Option(true, $"Plus jouée par le top ({Percent(share)}) que {current.Name} ({Percent(currentShare)}).");
            }

            if (gain >= BigGain && share >= currentShare)
            {
                return Option(true, $"+{gain} niveaux d'objet{atMax} sur {current.Name}, et au moins aussi jouée par le top.");
            }

            return Option(false, share >= MetaShare ? "Jouée par le top, mais pas au-dessus de ta pièce." : "Peu jouée par le top.");
        }

        if (gain <= 0)
        {
            return Option(false, "Pas plus haute que la pièce portée, une fois les deux améliorées au max.");
        }

        if (current.SetId is > 0 && !isSet && gain < BigGain)
        {
            return Option(false, $"+{gain}, mais {current.Name} compte pour ton bonus de set.");
        }

        if (current.Embellished && !candidate.Embellished && gain < BigGain)
        {
            return Option(false, $"+{gain}, mais {current.Name} porte un embellissement.");
        }

        return Option(true, isSet && current.SetId is not > 0
            ? $"+{gain} niveaux d'objet{atMax}, et pièce de set."
            : $"+{gain} niveaux d'objet{atMax} sur {current.Name}.");
    }

    private static VaultReview Vault(ImportedVault vault, IReadOnlyList<ImportedGear> equipped, SpecMetaSnapshot meta, IReadOnlyDictionary<int, string> icons, UpgradeScale scale)
    {
        var activities = vault.Activities ?? [];
        var tracks = activities
            .Where(a => a.Threshold > 0)
            .GroupBy(a => a.Type)
            .OrderBy(g => g.Key switch { 3 => 0, 1 => 1, _ => 2 })
            .Select(g =>
            {
                var (label, unit) = g.Key switch
                {
                    1 => ("Donjons", "clé"),
                    3 => ("Raid", "boss"),
                    6 => ("Monde", "activité"),
                    _ => ("Autre", "activité")
                };
                return new VaultTrack(label, unit, g.OrderBy(a => a.Index)
                    .Select(a => new VaultSlotView(a.Threshold, Math.Min(a.Progress, a.Threshold), a.Progress >= a.Threshold ? a.ExampleIlvl : null))
                    .ToList());
            })
            .ToList();

        var choices = activities
            .SelectMany(a => a.Items ?? [])
            .Where(i => GearRules.SlotOf(i.Slot) is not null)
            .Select(i => Evaluate(i, equipped, meta, icons, scale))
            .OrderByDescending(o => o.Recommended)
            .ThenByDescending(o => o.TopShare)
            .ThenByDescending(o => o.Gain)
            .ToList();

        string advice;
        if (choices.Count > 0)
        {
            var best = choices[0];
            advice = best.Recommended
                ? $"À prendre : {best.Name} ({best.ItemLevel}{(best.MaxItemLevel > best.ItemLevel ? $", {best.MaxItemLevel} au max" : "")}). {best.Reason}"
                : "Aucune option n'améliore ton équipement : prends la plus haute pour la recycler en écus, ou la plus jouée par le top.";
            choices = choices.Select((c, i) => i == 0 ? c : c with { Recommended = false }).ToList();
        }
        else if (vault.HasRewards)
        {
            advice = "Ton coffre de la semaine passée t'attend : ouvre-le en jeu, puis refais l'export pour savoir quoi choisir.";
        }
        else
        {
            var unlocked = tracks.Sum(t => t.Slots.Count(s => s.Unlocked));
            var total = tracks.Sum(t => t.Slots.Count);
            var next = tracks
                .Select(t => (Track: t, Slot: t.Slots.FirstOrDefault(s => !s.Unlocked)))
                .Where(x => x.Slot is not null)
                .Select(x => (x.Track, Left: x.Slot!.Threshold - x.Slot.Progress))
                .OrderBy(x => x.Left)
                .FirstOrDefault();
            var best = tracks.SelectMany(t => t.Slots).Max(s => s.ItemLevel);
            advice = $"{unlocked}/{total} cases débloquées" + (best is { } ilvl ? $", jusqu'au niveau d'objet {ilvl}" : "") + "."
                     + (next.Track is { } track ? $" Prochaine case : encore {next.Left} {track.Unit}{Plural(next.Left)} ({track.Label.ToLowerInvariant()})." : "");
        }

        return new VaultReview(choices.Count > 0, tracks, choices, advice);
    }

    private static List<ConsumableCheck> Consumables(IReadOnlyList<ImportedConsumable> items, PulseRole role, int classId, IReadOnlyDictionary<int, string> icons)
    {
        var byKind = items
            .Where(i => i.Count > 0)
            .Select(i => (Item: i, Kind: KindOf(i)))
            .Where(x => x.Kind is not null)
            .GroupBy(x => x.Kind!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Item).ToList());

        var checks = new List<ConsumableCheck>
        {
            Check(ConsumableKind.Flask, "Flacon", Verdict.Problem, "Un flacon tient toute la clé : c'est la stat la moins chère du jeu."),
            Check(ConsumableKind.CombatPotion, "Potion de combat", Verdict.Problem, "Une potion par combat de boss au minimum, à coupler avec tes temps forts.", LowPotionStock),
            Check(ConsumableKind.HealthPotion, "Potion de soins", Verdict.Improve, "Elle sauve une clé sur un mauvais placement.", 3),
            Check(ConsumableKind.Food, "Nourriture", Verdict.Improve, "Repas de stats avant la clé (ou festin du groupe).")
        };

        // Chaman et voleur ont leurs propres améliorations d'arme (imprégnations, poisons).
        if (classId is not (ShamanClass or RogueClass))
        {
            checks.Add(Check(ConsumableKind.WeaponBuff, "Huile ou pierre d'arme", Verdict.Improve, "Bonus d'arme d'une heure, à refaire après une mort."));
        }

        if (role.Kind == RoleKind.Healer)
        {
            checks.Add(Check(ConsumableKind.ManaPotion, "Potion de mana", Verdict.Improve, "Pour les fins de combat longues."));
        }

        checks.Add(Check(ConsumableKind.AugmentRune, "Rune d'augmentation", Verdict.Good, "Petit bonus de stat principale, facultatif."));
        return checks;

        ConsumableCheck Check(ConsumableKind kind, string label, Verdict ifMissing, string why, int lowStock = 0)
        {
            if (!byKind.TryGetValue(kind, out var found))
            {
                return new ConsumableCheck(kind, label, ifMissing, 0, null, null, 0, null,
                    $"Rien dans les sacs. {why}");
            }

            var current = found.Where(i => i.Current != false).ToList();
            var best = (current.Count > 0 ? current : found).OrderByDescending(i => i.Quality ?? 0).ThenByDescending(i => i.Count).First();
            var count = found.Sum(i => i.Count);
            var (verdict, advice) = current.Count == 0
                ? (Verdict.Improve, "D'une ancienne extension : bien plus faible que l'actuelle.")
                : lowStock > 0 && count < lowStock
                    ? (Verdict.Improve, $"Stock bas ({count}) : prévois-en au moins {lowStock}.")
                    : (Verdict.Good, count > 1 ? $"{count} en stock." : "En stock.");
            return new ConsumableCheck(kind, label, verdict, best.Id, best.Name, icons.GetValueOrDefault(best.Id), count, best.Quality, advice);
        }
    }

    /// <summary>Type d'un consommable : sa sous-classe, puis son effet « Utiliser : » (français ou anglais) pour les potions.</summary>
    internal static ConsumableKind? KindOf(ImportedConsumable item)
    {
        var use = item.Use ?? "";
        var name = item.Name ?? "";
        bool Mentions(params string[] words) => words.Any(w => use.Contains(w, StringComparison.OrdinalIgnoreCase));

        return item.Kind switch
        {
            "flask" => ConsumableKind.Flask,
            "potion" when Mentions("mana") && !Mentions("santé", "health", "points de vie") => ConsumableKind.ManaPotion,
            "potion" when Mentions("santé", "health", "points de vie", "vie") && !Mentions("augmente", "increases") => ConsumableKind.HealthPotion,
            "potion" => ConsumableKind.CombatPotion,
            "food" when Mentions("nourri", "well fed", "caractéristique", "stat", "secondaire", "secondary") || name.Contains("festin", StringComparison.OrdinalIgnoreCase) || name.Contains("feast", StringComparison.OrdinalIgnoreCase) => ConsumableKind.Food,
            "enhancement" when Mentions("arme", "weapon") => ConsumableKind.WeaponBuff,
            "other" or "enhancement" when name.Contains("rune", StringComparison.OrdinalIgnoreCase) => ConsumableKind.AugmentRune,
            _ => null
        };
    }

    private static string Percent(double share) => $"{Math.Round(share * 100):0} %";

    private static string Plural(int count) => count > 1 ? "s" : "";
}
