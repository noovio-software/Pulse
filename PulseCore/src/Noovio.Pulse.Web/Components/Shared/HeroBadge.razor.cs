using Microsoft.AspNetCore.Components;

namespace Noovio.Pulse.Web.Components.Shared;

public partial class HeroBadge
{
    [Parameter, EditorRequired] public string Name { get; set; } = "";
    [Parameter] public string? IconUrl { get; set; }
    [Parameter] public bool Small { get; set; }
}
