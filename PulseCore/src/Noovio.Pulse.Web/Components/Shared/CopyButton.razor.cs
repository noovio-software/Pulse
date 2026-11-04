using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Noovio.Pulse.Web.Components.Shared;

public partial class CopyButton
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter, EditorRequired] public string? Text { get; set; }
    [Parameter] public string Label { get; set; } = "Copier";
    [Parameter] public bool Primary { get; set; }

    /// <summary>Bouton carré sans texte ; le libellé passe en infobulle.</summary>
    [Parameter] public bool IconOnly { get; set; }

    private bool copied;
    private bool failed;

    private async Task CopyAsync()
    {
        copied = await JS.InvokeAsync<bool>("pulse.copy", Text);
        failed = !copied;
        if (copied)
        {
            StateHasChanged();
            await Task.Delay(TimeSpan.FromSeconds(2));
            copied = false;
        }
    }
}
