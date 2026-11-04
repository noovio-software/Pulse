using Microsoft.AspNetCore.Components;
using Noovio.FrontPulse.Application.Analysis;

namespace Noovio.FrontPulse.Web.Components.Analysis;

public partial class FarmTargetRow
{
    [Parameter, EditorRequired] public FarmTarget Target { get; set; } = default!;
}
