using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Application.Analysis;

namespace Noovio.Pulse.Web.Components.Analysis;

public partial class FindingCard
{
    [Parameter, EditorRequired] public AnalysisFinding Finding { get; set; } = default!;
}
