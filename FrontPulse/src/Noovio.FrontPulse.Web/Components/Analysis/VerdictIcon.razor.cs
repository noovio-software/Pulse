using Microsoft.AspNetCore.Components;
using Noovio.FrontPulse.Application.Analysis;

namespace Noovio.FrontPulse.Web.Components.Analysis;

public partial class VerdictIcon
{
    [Parameter, EditorRequired] public Verdict Verdict { get; set; }
    [Parameter] public int Size { get; set; } = 26;
}
