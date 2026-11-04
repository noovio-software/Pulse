using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Application.Analysis;

namespace Noovio.Pulse.Web.Components.Analysis;

public partial class FarmTargetRow
{
    [Parameter, EditorRequired] public FarmTarget Target { get; set; } = default!;
}
