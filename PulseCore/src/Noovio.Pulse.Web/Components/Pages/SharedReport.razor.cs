using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Analysis;
using Noovio.Pulse.Web.Components.Shared;
using Noovio.Pulse.Web.Localization;
using Noovio.Pulse.Web.Resources;
using Noovio.Pulse.Web.ShareCard;

namespace Noovio.Pulse.Web.Components.Pages;

/// <summary>
/// Analyse partagée par lien, ouverte à tous sans connexion : la copie figée faite au partage, en lecture seule
/// (ni relance, ni changement de spécialisation). Prérendue, pour l'aperçu du lien dans les messageries.
/// Les constats restent dans la langue de celui qui a partagé : ils ont été rédigés à l'analyse.
/// </summary>
public partial class SharedReport
{
    [Inject] private ISharedAnalysisStore SharedAnalyses { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public string Token { get; set; } = "";

    private SharedAnalysis? shared;
    private bool loading = true;
    private bool refreshTooltips;

    private string PageUrl => Navigation.ToAbsoluteUri($"partage/{Token}").AbsoluteUri;

    /// <summary>La vignette dans la langue de la page : son adresse la porte, l'image peut donc être gardée en cache.</summary>
    private string CardUrl => Navigation.ToAbsoluteUri(ShareCardRenderer.Path(Token, PulseLocalization.CurrentLanguage)).AbsoluteUri;

    private static string PreviewTitle(SharedAnalysis s) =>
        $"{s.Analysis.Character.Name} · {s.Analysis.Spec.SpecName} {s.Analysis.Spec.ClassName}";

    /// <summary>« Conformité à la méta 72/100 · Niv. d'objet 652,3 (+3 vs top) · M+ 3 120 · 3 problèmes, 2 à améliorer. Analyse du … ».</summary>
    private static string PreviewDescription(SharedAnalysis s)
    {
        var a = s.Analysis;
        var c = a.Character;
        var delta = c.Loadout.ItemLevel - a.Meta.MedianItemLevel;
        var parts = new List<string>
        {
            Ui.F(WebTexts.Shared_PreviewScore, a.Score),
            Ui.F(WebTexts.Shared_PreviewItemLevel, Ui.Ilvl(c.Loadout.ItemLevel), delta.ToString("+0.#;-0.#;0"))
        };
        if (c.MythicPlus is { } m)
        {
            parts.Add(Ui.F(WebTexts.Shared_PreviewMythicPlus, m.Score.ToString("N0")));
        }

        var problems = a.Findings.Count(f => f.Verdict == Verdict.Problem);
        var improve = a.Findings.Count(f => f.Verdict == Verdict.Improve);
        parts.Add(problems + improve == 0
            ? WebTexts.Shared_PreviewNothingToFix
            : $"{ShareCardRenderer.Problems(problems)}, {Ui.F(WebTexts.Card_Improve, improve)}");

        return $"{string.Join(" · ", parts)}. {Ui.F(WebTexts.Shared_AnalyzedOn, Ui.Date(a.AnalyzedAtUtc))}.";
    }

    protected override async Task OnParametersSetAsync()
    {
        if (shared?.Token == Token)
        {
            return;
        }

        loading = true;
        shared = await SharedAnalyses.GetAsync(Token, CancellationToken.None);
        loading = false;
        refreshTooltips = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (refreshTooltips)
        {
            refreshTooltips = false;
            await JS.InvokeVoidAsync("pulse.refreshTooltips");
        }
    }
}
