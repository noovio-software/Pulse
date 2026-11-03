using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Noovio.FrontPulse.Domain;
using Noovio.FrontPulse.Domain.Entities;
using Noovio.FrontPulse.Domain.Enums;

namespace Noovio.FrontPulse.Web.Components.Meta;

public partial class TalentCalculatorDialog
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter, EditorRequired] public TalentTreeLayout Layout { get; set; } = default!;
    [Parameter, EditorRequired] public TankSpecInfo Spec { get; set; } = default!;
    [Parameter] public int SampleSize { get; set; }

    /// <summary>Configuration la plus jouée, ouverte dans le calculateur Wowhead.</summary>
    [Parameter] public string? LoadoutCode { get; set; }

    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>Talents d'un joueur : l'arbre compare alors ses choix à ceux du top (analyse de personnage).</summary>
    [Parameter] public IReadOnlyCollection<int>? Selected { get; set; }

    /// <summary>Arbre héroïque ouvert d'emblée (celui du joueur, en mode comparaison).</summary>
    [Parameter] public string? InitialHero { get; set; }

    protected override void OnInitialized() => activeHero = InitialHero;

    private ElementReference dialog;
    private string? activeHero;
    private bool refreshTooltips = true;

    /// <summary>Déjà triées par popularité par l'agrégation.</summary>
    private IReadOnlyList<TalentTreeSection> HeroSections => Layout.Sections.Where(s => s.Tree == TalentTree.Hero).ToList();

    private TalentTreeSection? Section(TalentTree tree) => Layout.Sections.FirstOrDefault(s => s.Tree == tree && s.Nodes.Count > 0);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Le focus dans la fenêtre rend la touche Échap opérante tout de suite.
            await dialog.FocusAsync(preventScroll: true);
        }

        if (refreshTooltips)
        {
            refreshTooltips = false;
            await JS.InvokeVoidAsync("frontpulse.refreshTooltips");
        }
    }

    private void SelectHero(string name)
    {
        activeHero = name;
        refreshTooltips = true;
    }

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            await OnClose.InvokeAsync();
        }
    }
}
