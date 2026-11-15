using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Analysis;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Enums;
using Noovio.Pulse.Web.Resources;

namespace Noovio.Pulse.Web.Components.Analysis;

/// <summary>
/// Le rapport complet : synthèse, plan d'action, stats, gemmes et enchantements, farm, bijoux, talents, M+, équipement, fiche.
/// La page qui l'affiche charge l'analyse et fournit l'adresse de base (ancres, comparaison dans une autre spécialisation).
/// </summary>
public partial class AnalysisReport
{
    [Parameter, EditorRequired] public CharacterAnalysis Analysis { get; set; } = default!;

    /// <summary>Adresse de la page sans paramètre (« compte/personnages/12/analyse », « importer/… »).</summary>
    [Parameter, EditorRequired] public string BasePath { get; set; } = "";

    [Parameter] public string? RealmName { get; set; }

    /// <summary>Origine des données, affichée en puce (« Addon · il y a 5 min ») ; rien pour un personnage du compte.</summary>
    [Parameter] public string? Source { get; set; }

    [Parameter] public string? SourceTitle { get; set; }

    [Parameter] public EventCallback OnRefresh { get; set; }

    [Parameter] public string RefreshTitle { get; set; } = WebTexts.Report_RerunTitle;

    private bool treeOpen;

    private IReadOnlyList<AnalysisFinding> ToFix => Analysis.Findings.Where(f => f.Verdict != Verdict.Good).ToList();

    private IReadOnlyList<AnalysisFinding> Strengths => Analysis.Findings.Where(f => f.Verdict == Verdict.Good).ToList();

    /// <summary>Le plan d'action par effort demandé : ce qui se règle ce soir, puis ce qui demande du craft, du farm, des clés.</summary>
    private IReadOnlyList<PlanGroup> PlanGroups =>
    [
        .. new[]
        {
            new PlanGroup(WebTexts.Plan_Now, WebTexts.Plan_NowHint, ToFix.Where(f => f.Area is AnalysisAreaKind.Stats or AnalysisAreaKind.Gems or AnalysisAreaKind.Enchantments or AnalysisAreaKind.Talents).ToList()),
            new PlanGroup(WebTexts.Plan_Craft, WebTexts.Plan_CraftHint, ToFix.Where(f => f.Area is AnalysisAreaKind.Embellishments).ToList()),
            new PlanGroup(WebTexts.Plan_Farm, WebTexts.Plan_FarmHint, ToFix.Where(f => f.Area is AnalysisAreaKind.ItemLevel or AnalysisAreaKind.Trinkets).ToList()),
            new PlanGroup(WebTexts.Plan_Play, WebTexts.Plan_PlayHint, ToFix.Where(f => f.Area is AnalysisAreaKind.MythicPlus).ToList())
        }.Where(g => g.Findings.Count > 0)
    ];

    private string Anchor(string section) => $"{BasePath}?spec={Analysis.Spec.Slug}#{section}";

    private static string PrimaryStatName(CharacterSheet sheet) =>
        sheet.Intellect >= Math.Max(sheet.Strength, sheet.Agility) ? WebTexts.Sheet_Intellect : sheet.Strength >= sheet.Agility ? WebTexts.Sheet_Strength : WebTexts.Sheet_Agility;

    /// <summary>La spécialisation jouée, nommée dans la langue de l'interface plutôt que dans celle de l'API.</summary>
    private string PlayedSpecName =>
        Analysis.Character.ActiveSpecId is { } id && Enum.IsDefined((WowSpec)id) ? ((WowSpec)id).Info().SpecName
        : Analysis.Character.ActiveSpecName ?? WebTexts.Report_OtherSpec;

    private sealed record PlanGroup(string Title, string Hint, IReadOnlyList<AnalysisFinding> Findings);
}
