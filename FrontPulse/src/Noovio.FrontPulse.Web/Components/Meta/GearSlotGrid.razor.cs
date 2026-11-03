using Microsoft.AspNetCore.Components;
using Noovio.FrontPulse.Domain.Entities;

namespace Noovio.FrontPulse.Web.Components.Meta;

public partial class GearSlotGrid
{
    [Parameter, EditorRequired] public IReadOnlyList<SlotUsage> Slots { get; set; } = [];
}
