using Microsoft.AspNetCore.Components;
using Noovio.FrontPulse.Domain.Enums;

namespace Noovio.FrontPulse.Web.Components.Meta;

public partial class StatIcon
{
    [Parameter, EditorRequired] public SecondaryStat Stat { get; set; }
    [Parameter] public int Size { get; set; } = 18;
}
