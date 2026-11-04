using Microsoft.AspNetCore.Components;
using Noovio.FrontPulse.Application.Analysis;

namespace Noovio.FrontPulse.Web.Components.Analysis;

public partial class FarmRoadmapPanel
{
    [Parameter, EditorRequired] public FarmRoadmap Roadmap { get; set; } = default!;
}
