using Microsoft.AspNetCore.Components;

namespace AltPulse.Client.Components;

public partial class GridLabel
{
    /// <summary>Nom d'icône de <see cref="GridIcon"/>.</summary>
    [Parameter, EditorRequired] public string Icon { get; set; } = "";
    [Parameter, EditorRequired] public string Text { get; set; } = "";
}
