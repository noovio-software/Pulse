using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Web.Resources;

namespace Noovio.Pulse.Web.Components.Shared;

public partial class CompareButton
{
    /// <summary>Spécialisation d'où part la comparaison : la page du compte met ses personnages en avant.</summary>
    [Parameter] public SpecInfo? Spec { get; set; }

    /// <summary>Version courte, pour les fiches de l'accueil.</summary>
    [Parameter] public bool Compact { get; set; }

    private string Label => Compact ? WebTexts.Compare_Short : Ui.F(WebTexts.Compare_Long, Site.Words.Noun);

    private string AccountUrl => Spec is null ? "compte" : $"compte?spec={Spec.Slug}";
}
