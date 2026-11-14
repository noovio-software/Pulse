using System.Globalization;
using System.Text;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Enums;
using Noovio.Pulse.Domain.Roles;

namespace Noovio.Pulse.Application.Import;

/// <summary>
/// Traduit un export de l'addon dans le modèle lu chez Blizzard, pour que l'analyse le traite comme un personnage du compte.
/// Les textes viennent du client (sa langue) : ce qui se compare par nom (talents, arbre héroïque, embellissements)
/// est ramené aux noms de la méta par identifiant ; enchantements et gemmes se lisent en français comme en anglais.
/// </summary>
public static class ImportedCharacterMapper
{
    public static CharacterDetails ToDetails(
        CharacterImport import, PulseRole role, SpecInfo spec, SpecMetaSnapshot meta, TalentTreeLayout? layout, CharacterExtras extras)
    {
        var who = import.Character!;
        var gear = (import.Gear ?? []).Select(g => ToGear(g, meta)).OfType<GearPiece>().ToList();

        // Les talents exportés sont ceux de la spécialisation jouée : comparés à une autre, ils n'ont pas de sens.
        var talents = who.Spec == (int)spec.Spec ? Talents(import.Talents, layout, meta) : [];
        var heroTree = talents.Count == 0 ? null : HeroTree(talents, layout) ?? import.Talents?.Hero;
        var stats = ToStats(import.Stats, role.BasisOf(spec.Spec));

        return new CharacterDetails(
            who.Name!,
            RegionOf(who.Region),
            RealmSlug(who.Realm ?? who.RealmNormalized),
            who.Spec,
            who.SpecName,
            new CharacterLoadout(
                who.Spec,
                GearRules.AverageItemLevel(gear),
                gear,
                stats,
                talents,
                who.Spec == (int)spec.Spec ? import.Talents?.Code : null,
                heroTree),
            ToSheet(import.Sheet, stats, import.Stats?.Versatility?.Taken ?? 0, role),
            extras.PortraitUrl,
            extras.AvatarUrl,
            extras.MythicPlus);
    }

    /// <summary>Région Blizzard (us, eu…) ; eu par défaut, celle du public des sites.</summary>
    public static string RegionOf(string? region) =>
        region?.ToLowerInvariant() is { } r && r is "us" or "eu" or "kr" or "tw" or "cn" ? r : "eu";

    /// <summary>
    /// Slug d'un royaume comme Blizzard et Raider.io l'écrivent : « Conseil des Ombres » → conseil-des-ombres,
    /// « Chants éternels » → chants-eternels, « Arak-arahm » → arakarahm, « Pozzo dell'Eternità » → pozzo-delleternita.
    /// </summary>
    public static string RealmSlug(string? realm)
    {
        if (string.IsNullOrWhiteSpace(realm))
        {
            return "";
        }

        var builder = new StringBuilder();
        foreach (var c in realm.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark || c is '\'' or '’' or '-' or '(' or ')')
            {
                continue;
            }

            builder.Append(char.IsWhiteSpace(c) ? '-' : char.ToLowerInvariant(c));
        }

        return string.Join('-', builder.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
    }

    private static GearPiece? ToGear(ImportedGear g, SpecMetaSnapshot meta)
    {
        if (GearRules.SlotOf(g.Slot) is not { } slot || g.Id <= 0)
        {
            return null;
        }

        var name = string.IsNullOrWhiteSpace(g.Name) ? $"#{g.Id}" : g.Name;
        var effects = (g.Effects ?? []).Select(e => ToEffect(e, name)).ToList();
        var embellished = g.Embellished;
        if (embellished && EmbellishmentOf(g, effects, meta) is { } part)
        {
            // Nom de la méta en tête : l'analyse compare les embellissements par nom.
            effects.Insert(0, new ItemEffect(part.SpellId, part.Name, null));
        }

        var enchant = g.Enchant ?? (g.EnchantId is > 0 ? $"Enchantement #{g.EnchantId}" : null);

        return new GearPiece(
            slot,
            g.Id,
            name,
            g.Ilvl,
            g.Bonus ?? [],
            enchant is null ? [] : [enchant],
            effects,
            embellished,
            (g.Gems ?? []).Select(s => new GemSocket(s.Type ?? "PRISMATIC", s.Id is > 0 ? s.Id : null, s.Name, s.Text)).ToList(),
            (g.Stats ?? new Dictionary<string, double>()).Select(ToItemStat).OfType<ItemStat>().ToList());
    }

    /// <summary>Préfixe anglais remis devant le texte : l'analyse repère « Use: » comme « Utiliser : », quelle que soit la langue du client.</summary>
    private static ItemEffect ToEffect(ImportedEffect e, string itemName)
    {
        var prefix = e.Trigger?.ToLowerInvariant() switch
        {
            "use" => "Use: ",
            "equip" => "Equip: ",
            _ => ""
        };
        return new ItemEffect(e.Spell, string.IsNullOrWhiteSpace(e.Name) ? itemName : e.Name, e.Text is null ? null : prefix + e.Text);
    }

    /// <summary>L'embellissement de la méta joué sur la pièce : même sort, sinon même objet crafté.</summary>
    private static EmbellishmentPart? EmbellishmentOf(ImportedGear g, IReadOnlyList<ItemEffect> effects, SpecMetaSnapshot meta)
    {
        var parts = meta.Embellishments.SelectMany(c => c.Parts).ToList();
        return parts.FirstOrDefault(p => p.SpellId > 0 && effects.Any(e => e.SpellId == p.SpellId))
               ?? parts.FirstOrDefault(p => p.ItemId == g.Id);
    }

    private static ItemStat? ToItemStat(KeyValuePair<string, double> stat)
    {
        var type = stat.Key;
        SecondaryStat? secondary = type.Contains("HASTE", StringComparison.OrdinalIgnoreCase) ? SecondaryStat.Haste
            : type.Contains("VERSATILITY", StringComparison.OrdinalIgnoreCase) ? SecondaryStat.Versatility
            : type.Contains("MASTERY", StringComparison.OrdinalIgnoreCase) ? SecondaryStat.Mastery
            : type.Contains("CRIT", StringComparison.OrdinalIgnoreCase) ? SecondaryStat.Crit
            : null;
        return secondary is null || stat.Value <= 0 ? null : new ItemStat(secondary.Value, (int)Math.Round(stat.Value));
    }

    /// <summary>
    /// Chaque nœud exporté est retrouvé dans l'arbre de la spécialisation (identifiants de nœud communs au jeu et à l'API),
    /// l'option par son sort. Ce qui n'y figure pas est ignoré. Un nœud offert d'office n'est gardé que si le top le compte aussi.
    /// </summary>
    private static List<TalentPick> Talents(ImportedTalents? talents, TalentTreeLayout? layout, SpecMetaSnapshot meta)
    {
        if (talents?.Nodes is not { Count: > 0 } nodes || layout is null)
        {
            return [];
        }

        var byNode = layout.Sections
            .SelectMany(section => section.Nodes.Select(node => (section, node)))
            .GroupBy(x => x.node.NodeId)
            .ToDictionary(g => g.Key, g => g.First());
        var bySpell = layout.Sections
            .SelectMany(section => section.Nodes.SelectMany(node => node.Choices.Select(choice => (section, choice))))
            .Where(x => x.choice.SpellId > 0)
            .GroupBy(x => x.choice.SpellId)
            .ToDictionary(g => g.Key, g => g.First());
        var counted = meta.Talents.Select(t => (t.Tree, t.TalentId)).ToHashSet();

        var picks = new List<TalentPick>();
        foreach (var raw in nodes.Where(n => n.Count >= 4))
        {
            var (nodeId, spellId, rank, granted) = ((int)raw[0], (int)raw[2], (int)raw[3], raw.Count > 4 && raw[4] == 1);

            TalentTreeSection? section = null;
            TalentChoiceView? choice = null;
            if (byNode.TryGetValue(nodeId, out var found))
            {
                section = found.section;
                choice = found.node.Choices.Count == 1
                    ? found.node.Choices[0]
                    : found.node.Choices.FirstOrDefault(c => c.SpellId == spellId);
            }

            if (choice is null && spellId > 0 && bySpell.TryGetValue(spellId, out var bySpellId))
            {
                (section, choice) = bySpellId;
            }

            if (section is null || choice is null || (granted && !counted.Contains((section.Tree, choice.TalentId))))
            {
                continue;
            }

            picks.Add(new TalentPick(section.Tree, choice.TalentId, choice.SpellId, choice.Name, Math.Max(1, rank)));
        }

        return picks.DistinctBy(p => (p.Tree, p.TalentId)).ToList();
    }

    /// <summary>Nom (celui de la méta) de l'arbre héroïque dont viennent les talents héroïques choisis.</summary>
    private static string? HeroTree(IReadOnlyList<TalentPick> talents, TalentTreeLayout? layout)
    {
        var heroIds = talents.Where(t => t.Tree == TalentTree.Hero).Select(t => t.TalentId).ToHashSet();
        return heroIds.Count == 0
            ? null
            : layout?.Sections
                .Where(s => s.Tree == TalentTree.Hero)
                .OrderByDescending(s => s.Nodes.SelectMany(n => n.Choices).Count(c => heroIds.Contains(c.TalentId)))
                .FirstOrDefault()?.Name;
    }

    /// <summary>Comme la feuille Blizzard : crit et hâte de la façon de combattre de la spécialisation ; la polyvalence en jeu vaut pour les dégâts comme pour les soins.</summary>
    private static StatSheet? ToStats(ImportedStats? stats, StatBasis basis)
    {
        if (stats is null)
        {
            return null;
        }

        return new StatSheet(
            Haste: Line(stats.Haste),
            Versatility: new StatValue(stats.Versatility?.Rating ?? 0, stats.Versatility?.Done ?? 0),
            Mastery: new StatValue(stats.Mastery?.Rating ?? 0, stats.Mastery?.Percent ?? 0),
            Crit: Line(stats.Crit));

        StatValue Line(ImportedLines? line) => line is null
            ? new StatValue(0, 0)
            : new StatValue(line.Rating, basis switch
            {
                StatBasis.Spell => line.Spell,
                StatBasis.Ranged => line.Ranged,
                _ => line.Melee
            });
    }

    private static CharacterSheet? ToSheet(ImportedSheet? s, StatSheet? secondary, double versatilityTaken, PulseRole role) =>
        s is null || secondary is null
            ? null
            : new CharacterSheet(
                s.Health,
                s.Stamina,
                s.Strength,
                s.Agility,
                s.Intellect,
                role.Kind == RoleKind.Healer ? s.Mana : 0,
                s.Armor,
                secondary,
                versatilityTaken,
                s.Dodge,
                s.Parry,
                s.Block,
                s.Leech,
                s.Avoidance,
                s.Speed);
}
