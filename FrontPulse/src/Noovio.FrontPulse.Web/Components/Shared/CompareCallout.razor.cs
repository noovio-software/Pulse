using Microsoft.AspNetCore.Components;
using Noovio.FrontPulse.Domain;

namespace Noovio.FrontPulse.Web.Components.Shared;

public partial class CompareCallout
{
    /// <summary>Spécialisation de la page ; null sur l'accueil (toutes spécialisations).</summary>
    [Parameter] public TankSpecInfo? Spec { get; set; }
}
