using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Application.Analysis;
using Noovio.Pulse.Web.Components.Shared;
using Noovio.Pulse.Web.Resources;

namespace Noovio.Pulse.Web.Components.Analysis;

public partial class InventoryPanel
{
    [Parameter, EditorRequired] public InventoryReview Review { get; set; } = default!;

    private static string SlotTitle(VaultTrack track, VaultSlotView slot)
    {
        if (!slot.Unlocked)
            return Ui.F(WebTexts.Inventory_SlotProgress, slot.Progress, slot.Threshold, track.UnitFor(slot.Threshold));

        return slot.ItemLevel is int ilvl ? Ui.F(WebTexts.Inventory_SlotUnlockedIlvl, ilvl) : WebTexts.Inventory_SlotUnlocked;
    }
}
