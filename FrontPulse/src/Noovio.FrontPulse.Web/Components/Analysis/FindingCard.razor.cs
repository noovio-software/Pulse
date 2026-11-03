using Microsoft.AspNetCore.Components;
using Noovio.FrontPulse.Application.Analysis;

namespace Noovio.FrontPulse.Web.Components.Analysis;

public partial class FindingCard
{
    [Parameter, EditorRequired] public AnalysisFinding Finding { get; set; } = default!;
}
