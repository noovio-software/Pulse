using Microsoft.AspNetCore.Components;
using Noovio.FrontPulse.Domain;

namespace Noovio.FrontPulse.Web.Components.Shared;

public partial class CompareButton
{
    /// <summary>Spécialisation d'où part la comparaison : la page du compte met ses personnages en avant.</summary>
    [Parameter] public TankSpecInfo? Spec { get; set; }

    /// <summary>Version courte, pour les fiches de l'accueil.</summary>
    [Parameter] public bool Compact { get; set; }

    private string Label => Compact ? "Comparer" : "Comparer mon tank";

    private string AccountUrl => Spec is null ? "compte" : $"compte?spec={Spec.Slug}";
}
