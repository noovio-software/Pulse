using AltPulse.Client.Models;
using Microsoft.AspNetCore.Components;

namespace AltPulse.Client.Components;

public partial class VaultRow
{
    [Parameter, EditorRequired] public string Icon { get; set; } = "";
    [Parameter, EditorRequired] public string Label { get; set; } = "";
    [Parameter, EditorRequired] public IReadOnlyList<CharacterColumn> Columns { get; set; } = [];
    [Parameter, EditorRequired] public Func<CharacterSnapshot, IReadOnlyList<VaultSlot>> Slots { get; set; } = _ => [];

    /// <summary>Unité de progression affichée en infobulle (« boss », « clé »).</summary>
    [Parameter] public string Unit { get; set; } = "";

    /// <summary>Ligne de la semaine passée : cases débloquées en violet.</summary>
    [Parameter] public bool Previous { get; set; }

    private string SlotTooltip(VaultSlot slot) =>
        slot.Reward is null ? $"{slot.Progress}/{slot.Threshold} {Unit}(s)" : $"Débloqué : {slot.Reward}";
}
