using Microsoft.AspNetCore.Components;

namespace PulseMetrics.Alt.Client.Components;

public partial class GridLabel
{
    /// <summary>Nom d'icône de <see cref="GridIcon"/>.</summary>
    [Parameter, EditorRequired] public string Icon { get; set; } = "";
    [Parameter, EditorRequired] public string Text { get; set; } = "";
}
