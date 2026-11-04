using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Domain.Entities;

namespace Noovio.Pulse.Web.Components.Meta;

public partial class GearSlotGrid
{
    [Parameter, EditorRequired] public IReadOnlyList<SlotUsage> Slots { get; set; } = [];
}
