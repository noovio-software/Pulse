using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Entities;

namespace Noovio.Pulse.Web.Components.Meta;

public partial class TalentBuildPanel
{
    [Parameter, EditorRequired] public SpecInfo Spec { get; set; } = default!;
    [Parameter, EditorRequired] public IReadOnlyList<TalentBuild> Builds { get; set; } = [];
    [Parameter] public IReadOnlyList<HeroTreeUsage> HeroTrees { get; set; } = [];

    /// <summary>null tant qu'aucune ingestion n'a récupéré l'arbre : le calculateur reste désactivé.</summary>
    [Parameter] public TalentTreeLayout? Layout { get; set; }

    [Parameter] public int SampleSize { get; set; }

    private bool calculatorOpen;
}
