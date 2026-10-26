using AltPulse.Client.Models;
using Microsoft.AspNetCore.Components;

namespace AltPulse.Client.Components;

public partial class CharacterGrid
{
    [Parameter] public SeasonInfo? Season { get; set; }
    [Parameter, EditorRequired] public IReadOnlyList<CharacterColumn> Columns { get; set; } = [];
    [Parameter] public string? HighlightKey { get; set; }
    [Parameter] public string SortBy { get; set; } = "recent";
    [Parameter] public EventCallback<string> OnSortChanged { get; set; }
    [Parameter] public ISet<string> CollapsedSections { get; set; } = new HashSet<string>();
    [Parameter] public EventCallback<string> OnToggleSection { get; set; }
    [Parameter] public EventCallback<CharacterColumn> OnRefresh { get; set; }
    [Parameter] public EventCallback<CharacterColumn> OnRemove { get; set; }
    [Parameter] public EventCallback<CharacterColumn> OnShowEquipment { get; set; }

    /// <summary>Colonne des libellés réduite aux icônes (le texte reste en infobulle via title).</summary>
    [Parameter] public bool Compact { get; set; }
    [Parameter] public EventCallback OnToggleCompact { get; set; }

    private static readonly Func<CharacterSnapshot, IReadOnlyList<VaultSlot>> RaidSlots = c => c.Vault.Raids;
    private static readonly Func<CharacterSnapshot, IReadOnlyList<VaultSlot>> DungeonSlots = c => c.Vault.Dungeons;
    private static readonly Func<CharacterSnapshot, IReadOnlyList<VaultSlot>> PreviousWeekSlots = c => c.Vault.PreviousWeekDungeons;

    private string DungeonsTitle => $"Mythique+ · {Season?.Name}";

    private string SortLabel => Ui.SortOptions.FirstOrDefault(o => o.Value == SortBy).Label ?? "";

    private bool IsCollapsed(string section) => CollapsedSections.Contains(section);

    private Task OnSortSelected(ChangeEventArgs e) => OnSortChanged.InvokeAsync(e.Value?.ToString() ?? "recent");

    private static string BossTooltip(RaidInfo raid, bool[] kills) =>
        string.Join("\n", raid.Encounters.Select((e, i) => $"{(i < kills.Length && kills[i] ? "✔" : "·")} {e.Name}"));
}
