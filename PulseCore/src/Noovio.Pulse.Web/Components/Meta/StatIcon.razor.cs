using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Domain.Enums;

namespace Noovio.Pulse.Web.Components.Meta;

public partial class StatIcon
{
    [Parameter, EditorRequired] public SecondaryStat Stat { get; set; }
    [Parameter] public int Size { get; set; } = 18;
}
