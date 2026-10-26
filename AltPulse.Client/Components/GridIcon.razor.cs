using Microsoft.AspNetCore.Components;

namespace AltPulse.Client.Components;

public partial class GridIcon
{
    /// <summary>users, sort, collapse, level, ilvl, rating, key, raid, dungeon, world, history, progress, clock.</summary>
    [Parameter, EditorRequired] public string Name { get; set; } = "";
    [Parameter] public int Size { get; set; } = 18;
}
