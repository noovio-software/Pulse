using System.Globalization;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Analysis;
using Noovio.Pulse.Application.Routes;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Enums;
using Noovio.Pulse.Web.Resources;

namespace Noovio.Pulse.Web.Components.Shared;

public static class Ui
{
    /// <summary>Slug Raider.io de la saison (« season-mn-1 ») → « Saison 1 » ; tel quel s'il a une autre forme.</summary>
    public static string SeasonLabel(string season) =>
        season.Split('-') is [.., var last] && int.TryParse(last, out var number) ? F(WebTexts.Ui_Season, number) : season;

    /// <summary>Forces ennemies de la route par rapport au minimum du donjon → « 101 % ».</summary>
    public static string ForcesPercent(DungeonRoute route) =>
        route.EnemyForcesRequired > 0 ? Percent((double)route.EnemyForces / route.EnemyForcesRequired) : "-";

    /// <summary>Durée de la clé → « 30 min ».</summary>
    public static string KeyTimer(int seconds) => $"{seconds / 60} min";

    public static string HighlightLabel(RouteHighlight highlight) => highlight switch
    {
        RouteHighlight.MostPopular => WebTexts.Ui_Highlight_MostPopular,
        RouteHighlight.TankAuthored => WebTexts.Ui_Highlight_TankAuthored,
        RouteHighlight.BiggestPulls => WebTexts.Ui_Highlight_BiggestPulls,
        _ => WebTexts.Ui_Highlight_LowestPeak
    };

    public static string HighlightCss(RouteHighlight highlight) => highlight switch
    {
        RouteHighlight.MostPopular => "is-popular",
        RouteHighlight.TankAuthored => "is-tank",
        _ => "is-pulls"
    };

    /// <summary>En-tête d'un emplacement : « Tête », « Anneaux ».</summary>
    public static string SlotLabel(GearSlot slot) => GameLabels.SlotGroup(slot);

    public static string StatLabel(SecondaryStat stat) => GameLabels.Stat(stat);

    public static string StatCss(SecondaryStat stat) => $"stat-{stat.ToString().ToLowerInvariant()}";

    public static string TreeLabel(TalentTree tree) => tree switch
    {
        TalentTree.Class => WebTexts.Ui_Tree_Class,
        TalentTree.Spec => WebTexts.Ui_Tree_Spec,
        _ => WebTexts.Ui_Tree_Hero
    };

    /// <summary>« soigneurs » → « Soigneurs ».</summary>
    public static string Capitalized(string word) => word.Length == 0 ? word : char.ToUpper(word[0], CultureInfo.CurrentCulture) + word[1..];

    /// <summary>Part dans [0, 1] → « 42 % » en français, « 42% » en anglais.</summary>
    public static string Percent(double share, int decimals = 0) => share.ToString("P" + decimals, CultureInfo.CurrentCulture);

    /// <summary>Valeur déjà en pourcentage (24.3) → « 24,3 % » / « 24.3% ».</summary>
    public static string PercentValue(double percent, int decimals = 0) => Percent(percent / 100, decimals);

    /// <summary>Formate une ressource dans la culture de l'interface.</summary>
    public static string F(string format, params object?[] args) => string.Format(CultureInfo.CurrentCulture, format, args);

    /// <summary>Part dans [0, 1] → « 42.5% », pour une largeur CSS (indépendant de la culture).</summary>
    public static string CssPercent(double share) => (share * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    public static string Ilvl(double value) => value > 0 ? value.ToString("0.#") : "-";

    /// <summary>Icône de l'arbre héroïque portant ce nom, si l'arbre de talents est connu.</summary>
    public static string? HeroIcon(TalentTreeLayout? layout, string? heroTree) =>
        layout?.Sections.FirstOrDefault(s => s.Tree == TalentTree.Hero && s.Name == heroTree)?.IconUrl;

    public static string Ago(DateTimeOffset at)
    {
        var elapsed = DateTimeOffset.UtcNow - at;
        return elapsed.TotalMinutes < 1 ? WebTexts.Ui_JustNow
            : elapsed.TotalHours < 1 ? F(WebTexts.Ui_AgoMinutes, (int)elapsed.TotalMinutes)
            : elapsed.TotalDays < 1 ? F(WebTexts.Ui_AgoHours, (int)elapsed.TotalHours)
            : F(WebTexts.Ui_AgoDays, (int)elapsed.TotalDays);
    }

    private static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

    /// <summary>
    /// Date absolue à l'heure de Paris → « 20 novembre 2026 à 14:32 », « November 20, 2026 at 14:32 »
    /// (un lien partagé se relit des semaines après : « il y a 3 j » ne dirait plus grand-chose).
    /// </summary>
    public static string Date(DateTimeOffset at) =>
        TimeZoneInfo.ConvertTime(at, Paris).ToString(WebTexts.Ui_DateTimeFormat, CultureInfo.CurrentCulture);

    /// <summary>Durée → « 2 h 15 », « 40 min », « moins d'une minute ».</summary>
    public static string Duration(TimeSpan duration) =>
        duration.TotalMinutes < 1 ? WebTexts.Ui_DurationUnderMinute
            : duration.TotalHours < 1 ? F(WebTexts.Ui_DurationMinutes, (int)duration.TotalMinutes)
            : F(WebTexts.Ui_DurationHours, (int)duration.TotalHours, duration.Minutes);

    /// <summary>Durée restante → « dans 2 h 15 », « dans 40 min ».</summary>
    public static string In(TimeSpan remaining) =>
        remaining.TotalHours < 1 ? F(WebTexts.Ui_InMinutes, Math.Max(1, (int)remaining.TotalMinutes))
            : remaining.Minutes == 0 ? F(WebTexts.Ui_InHours, (int)remaining.TotalHours)
            : F(WebTexts.Ui_InHoursMinutes, (int)remaining.TotalHours, remaining.Minutes);

    /// <summary>Nombre de pixels CSS, indépendant de la culture (« 12.5px »).</summary>
    public static string Px(double value) => value.ToString("0.#", CultureInfo.InvariantCulture) + "px";

    /// <summary>Le calculateur Wowhead importe directement une chaîne d'export Blizzard.</summary>
    public static string TalentCalculatorUrl(string loadoutCode) => $"{WowheadSite}/talent-calc/blizzard/{loadoutCode}";

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
        Verdict.Good => WebTexts.Ui_Verdict_Good,
        Verdict.Improve => WebTexts.Ui_Verdict_Improve,
        _ => WebTexts.Ui_Verdict_Problem
    };

    public static string AreaLabel(AnalysisAreaKind area) => area switch
    {
        AnalysisAreaKind.ItemLevel => WebTexts.Ui_Area_ItemLevel,
        AnalysisAreaKind.Stats => WebTexts.Ui_Area_Stats,
        AnalysisAreaKind.Talents => WebTexts.Ui_Area_Talents,
        AnalysisAreaKind.Trinkets => WebTexts.Ui_Area_Trinkets,
        AnalysisAreaKind.Embellishments => WebTexts.Ui_Area_Embellishments,
        AnalysisAreaKind.Enchantments => WebTexts.Ui_Area_Enchantments,
        AnalysisAreaKind.Gems => WebTexts.Ui_Area_Gems,
        _ => WebTexts.Ui_Area_MythicPlus
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

    public static string InstanceKindLabel(InstanceKind kind) => kind == InstanceKind.Raid ? WebTexts.Ui_Instance_Raid : WebTexts.Ui_Instance_Dungeon;

    /// <summary>« +1,2 % Hâte ».</summary>
    public static string StatDelta(StatDelta delta) => F(WebTexts.Ui_StatDelta, delta.Percent, StatLabel(delta.Stat));

    /// <summary>« +70 Hâte · +30 Maîtrise » ; null si le texte ne donne aucune secondaire.</summary>
    public static string? StatList(IReadOnlyList<ItemStat> stats) =>
        stats.Count == 0 ? null : string.Join(" · ", stats.Select(s => $"+{s.Value} {StatLabel(s.Stat)}"));

    /// <summary>Wowhead dans la langue de l'interface : « https://www.wowhead.com/fr » ou « https://www.wowhead.com ».</summary>
    public static string WowheadSite => WowheadLocale is { } locale ? $"https://www.wowhead.com/{locale}" : "https://www.wowhead.com";

    /// <summary>Sous-domaine de langue de Wowhead pour les infobulles ; aucun pour l'anglais (le site par défaut).</summary>
    private static string? WowheadLocale => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
    {
        "fr" => "fr",
        _ => null
    };

    /// <summary>Paramètre <c>domain</c> des infobulles Wowhead (vide en anglais), à la suite des autres.</summary>
    public static string WowheadDomain => WowheadLocale is { } locale ? $"&domain={locale}" : "";

    public static string ItemUrl(int itemId) => $"{WowheadSite}/item={itemId}";

    public static string ItemUrl(ItemStatSnapshot item) => ItemUrl(item.ItemId);

    public static string SpellUrl(int spellId) => $"{WowheadSite}/spell={spellId}";

    public static string ItemWowhead(int itemId) => $"item={itemId}{WowheadDomain}";

    public static string ItemWowhead(ItemStatSnapshot item) => $"item={item.ItemId}&ilvl={item.MedianItemLevel}{WowheadDomain}";

    public static string SpellWowhead(int spellId) => $"spell={spellId}{WowheadDomain}";
}
