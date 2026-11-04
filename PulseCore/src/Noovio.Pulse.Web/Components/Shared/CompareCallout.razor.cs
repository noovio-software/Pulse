using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Domain;

namespace Noovio.Pulse.Web.Components.Shared;

public partial class CompareCallout
{
    /// <summary>Spécialisation de la page ; null sur l'accueil (toutes spécialisations).</summary>
    [Parameter] public SpecInfo? Spec { get; set; }
}
