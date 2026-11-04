using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Application.Analysis;

namespace Noovio.Pulse.Web.Components.Analysis;

public partial class VerdictIcon
{
    [Parameter, EditorRequired] public Verdict Verdict { get; set; }
    [Parameter] public int Size { get; set; } = 26;
}
