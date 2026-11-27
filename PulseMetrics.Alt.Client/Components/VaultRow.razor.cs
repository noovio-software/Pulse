using PulseMetrics.Alt.Client.Localization;
using PulseMetrics.Alt.Client.Models;
using PulseMetrics.Alt.Client.Resources;
using Microsoft.AspNetCore.Components;

namespace PulseMetrics.Alt.Client.Components;

public partial class VaultRow
{
    [Parameter, EditorRequired] public string Icon { get; set; } = "";
    [Parameter, EditorRequired] public string Label { get; set; } = "";
    [Parameter, EditorRequired] public IReadOnlyList<CharacterColumn> Columns { get; set; } = [];
    [Parameter, EditorRequired] public Func<CharacterSnapshot, IReadOnlyList<VaultSlot>> Slots { get; set; } = _ => [];

    /// <summary>Unité de progression affichée en infobulle, au singulier et au pluriel (« clé », « clés »).</summary>
    [Parameter] public string Unit { get; set; } = "";

    [Parameter] public string Units { get; set; } = "";

    /// <summary>Ligne de la semaine passée : cases débloquées en violet.</summary>
    [Parameter] public bool Previous { get; set; }

    private string SlotTooltip(VaultSlot slot) =>
        slot.Reward is null
            ? AltLocalization.F(AltTexts.Grid_SlotProgress, slot.Progress, slot.Threshold, slot.Threshold == 1 ? Unit : Units)
            : AltLocalization.F(AltTexts.Grid_SlotUnlocked, slot.Reward);
}
