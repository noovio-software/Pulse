using Microsoft.AspNetCore.Components;

namespace PulseMetrics.Alt.Client.Components;

public partial class TopBar
{
    /// <summary>Actions propres à la page, alignées à droite de l'en-tête.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
