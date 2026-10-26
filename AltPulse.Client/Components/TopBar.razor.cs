using Microsoft.AspNetCore.Components;

namespace AltPulse.Client.Components;

public partial class TopBar
{
    /// <summary>Actions propres à la page, alignées à droite de l'en-tête.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
