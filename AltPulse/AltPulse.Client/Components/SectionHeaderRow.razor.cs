using Microsoft.AspNetCore.Components;

namespace AltPulse.Client.Components;

public partial class SectionHeaderRow
{
    /// <summary>Identifiant persistant de la section (mémorisé dans les préférences quand elle est repliée).</summary>
    [Parameter, EditorRequired] public string Id { get; set; } = "";
    [Parameter, EditorRequired] public string Title { get; set; } = "";
    [Parameter] public int ColumnCount { get; set; } = 1;
    [Parameter] public ISet<string> CollapsedSections { get; set; } = new HashSet<string>();
    [Parameter] public EventCallback<string> OnToggle { get; set; }

    private bool IsCollapsed => CollapsedSections.Contains(Id);

    private Task ToggleAsync() => OnToggle.InvokeAsync(Id);
}
