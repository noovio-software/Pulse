using System.Globalization;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Analysis;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Enums;

namespace Noovio.Pulse.Web.Components.Shared;

public static class Ui
{
    /// <summary>Slug Raider.io de la saison (« season-mn-1 ») → « Saison 1 » ; tel quel s'il a une autre forme.</summary>
    public static string SeasonLabel(string season) =>
        season.Split('-') is [.., var last] && int.TryParse(last, out var number) ? $"Saison {number}" : season;

    public static string SlotLabel(GearSlot slot) => slot switch
    {
        GearSlot.Head => "Tête",
        GearSlot.Neck => "Cou",
        GearSlot.Shoulder => "Épaules",
        GearSlot.Back => "Dos",
        GearSlot.Chest => "Torse",
        GearSlot.Wrist => "Poignets",
        GearSlot.Hands => "Mains",
        GearSlot.Waist => "Taille",
        GearSlot.Legs => "Jambes",
        GearSlot.Feet => "Pieds",
        GearSlot.Finger => "Anneaux",
        GearSlot.Trinket => "Bijoux",
        GearSlot.MainHand => "Main droite",
        _ => "Main gauche"
    };

    public static string StatLabel(SecondaryStat stat) => stat switch
    {
        SecondaryStat.Haste => "Hâte",
        SecondaryStat.Versatility => "Polyvalence",
        SecondaryStat.Mastery => "Maîtrise",
        _ => "Critique"
    };

    public static string StatCss(SecondaryStat stat) => $"stat-{stat.ToString().ToLowerInvariant()}";

    public static string TreeLabel(TalentTree tree) => tree switch
    {
        TalentTree.Class => "Classe",
        TalentTree.Spec => "Spécialisation",
        _ => "Héros"
    };

    /// <summary>« soigneurs » → « Soigneurs ».</summary>
    public static string Capitalized(string word) => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];

    /// <summary>Part dans [0, 1] → « 42 % ».</summary>
    public static string Percent(double share) => $"{share * 100:0} %";

    /// <summary>Part dans [0, 1] → « 42.5% », pour une largeur CSS (indépendant de la culture).</summary>
    public static string CssPercent(double share) => (share * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    public static string Ilvl(double value) => value > 0 ? value.ToString("0.#") : "-";

    /// <summary>Icône de l'arbre héroïque portant ce nom, si l'arbre de talents est connu.</summary>
    public static string? HeroIcon(TalentTreeLayout? layout, string? heroTree) =>
        layout?.Sections.FirstOrDefault(s => s.Tree == TalentTree.Hero && s.Name == heroTree)?.IconUrl;

    public static string Ago(DateTimeOffset at)
    {
        var elapsed = DateTimeOffset.UtcNow - at;
        return elapsed.TotalMinutes < 1 ? "à l'instant"
            : elapsed.TotalHours < 1 ? $"il y a {(int)elapsed.TotalMinutes} min"
            : elapsed.TotalDays < 1 ? $"il y a {(int)elapsed.TotalHours} h"
            : $"il y a {(int)elapsed.TotalDays} j";
    }

    /// <summary>Durée → « 2 h 15 », « 40 min », « moins d'une minute ».</summary>
    public static string Duration(TimeSpan duration) =>
        duration.TotalMinutes < 1 ? "moins d'une minute"
            : duration.TotalHours < 1 ? $"{(int)duration.TotalMinutes} min"
            : $"{(int)duration.TotalHours} h {duration.Minutes:00}";

    /// <summary>Durée restante → « dans 2 h 15 », « dans 40 min ».</summary>
    public static string In(TimeSpan remaining) =>
        remaining.TotalHours < 1 ? $"dans {Math.Max(1, (int)remaining.TotalMinutes)} min"
            : remaining.Minutes == 0 ? $"dans {(int)remaining.TotalHours} h"
            : $"dans {(int)remaining.TotalHours} h {remaining.Minutes:00}";

    /// <summary>Nombre de pixels CSS, indépendant de la culture (« 12.5px »).</summary>
    public static string Px(double value) => value.ToString("0.#", CultureInfo.InvariantCulture) + "px";

    /// <summary>Le calculateur Wowhead importe directement une chaîne d'export Blizzard.</summary>
    public static string TalentCalculatorUrl(string loadoutCode) => $"https://www.wowhead.com/fr/talent-calc/blizzard/{loadoutCode}";

    /// <summary>Quasi incontournable (≥ 90 %), courant (≥ 50 %), situationnel, jamais pris.</summary>
    public static string PickTier(double share) => share switch
    {
        >= 0.9 => "is-core",
        >= 0.5 => "is-common",
        > 0 => "is-situational",
        _ => "is-unpicked"
    };

    public static string VerdictCss(Verdict verdict) => verdict switch
    {
        Verdict.Good => "verdict-good",
        Verdict.Improve => "verdict-improve",
        _ => "verdict-problem"
    };

    public static string VerdictLabel(Verdict verdict) => verdict switch
    {
        Verdict.Good => "Bon",
        Verdict.Improve => "À améliorer",
        _ => "Problème"
    };

    public static string AreaLabel(AnalysisAreaKind area) => area switch
    {
        AnalysisAreaKind.ItemLevel => "Niveau d'objet",
        AnalysisAreaKind.Stats => "Caractéristiques",
        AnalysisAreaKind.Talents => "Talents",
        AnalysisAreaKind.Trinkets => "Bijoux",
        AnalysisAreaKind.Embellishments => "Embellissements",
        AnalysisAreaKind.Enchantments => "Enchantements",
        AnalysisAreaKind.Gems => "Gemmes",
        _ => "Mythique+"
    };

    /// <summary>Section du rapport qui détaille un domaine.</summary>
    public static string AreaSection(AnalysisAreaKind area) => area switch
    {
        AnalysisAreaKind.Stats or AnalysisAreaKind.Gems or AnalysisAreaKind.Enchantments => "optimiser",
        AnalysisAreaKind.ItemLevel => "farm",
        AnalysisAreaKind.Trinkets => "bijoux",
        AnalysisAreaKind.Talents => "talents",
        AnalysisAreaKind.MythicPlus => "mythique",
        _ => "plan"
    };

    public static string InstanceKindLabel(InstanceKind kind) => kind == InstanceKind.Raid ? "Raid" : "Donjon";

    /// <summary>« +1,2 % Hâte ».</summary>
    public static string StatDelta(StatDelta delta) => $"{delta.Percent:+0.0;-0.0} % {StatLabel(delta.Stat)}";

    /// <summary>« +70 Hâte · +30 Maîtrise » ; null si le texte ne donne aucune secondaire.</summary>
    public static string? StatList(IReadOnlyList<ItemStat> stats) =>
        stats.Count == 0 ? null : string.Join(" · ", stats.Select(s => $"+{s.Value} {StatLabel(s.Stat)}"));

    public static string ItemUrl(int itemId) => $"https://www.wowhead.com/fr/item={itemId}";

    public static string ItemUrl(ItemStatSnapshot item) => ItemUrl(item.ItemId);

    public static string SpellUrl(int spellId) => $"https://www.wowhead.com/fr/spell={spellId}";

    public static string ItemWowhead(int itemId) => $"item={itemId}&domain=fr";

    public static string ItemWowhead(ItemStatSnapshot item) => $"item={item.ItemId}&ilvl={item.MedianItemLevel}&domain=fr";

    public static string SpellWowhead(int spellId) => $"spell={spellId}&domain=fr";
}
