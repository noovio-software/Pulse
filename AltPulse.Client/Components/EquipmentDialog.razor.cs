using AltPulse.Client.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AltPulse.Client.Components;

public partial class EquipmentDialog
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter, EditorRequired] public CharacterSnapshot Character { get; set; } = default!;
    [Parameter] public EventCallback OnClose { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Les liens d'objets viennent d'être rendus : on demande à Wowhead d'y brancher ses infobulles.
            await JS.InvokeVoidAsync("altpulse.refreshTooltips");
        }
    }
}
