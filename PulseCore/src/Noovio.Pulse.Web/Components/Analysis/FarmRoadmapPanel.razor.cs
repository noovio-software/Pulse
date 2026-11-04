using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Application.Analysis;

namespace Noovio.Pulse.Web.Components.Analysis;

public partial class FarmRoadmapPanel
{
    [Parameter, EditorRequired] public FarmRoadmap Roadmap { get; set; } = default!;
}
