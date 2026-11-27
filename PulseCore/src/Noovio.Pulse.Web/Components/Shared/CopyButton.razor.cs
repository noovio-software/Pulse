using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Noovio.Pulse.Web.Resources;

namespace Noovio.Pulse.Web.Components.Shared;

public partial class CopyButton
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter, EditorRequired] public string? Text { get; set; }
    [Parameter] public string Label { get; set; } = WebTexts.Copy_Label;
    [Parameter] public bool Primary { get; set; }

    /// <summary>Bouton carré sans texte ; le libellé passe en infobulle.</summary>
    [Parameter] public bool IconOnly { get; set; }

    private bool copied;
    private bool failed;

    private async Task CopyAsync()
    {
        try
        {
            copied = await JS.InvokeAsync<bool>("pulse.copy", Text);
        }
        catch (JSDisconnectedException)
        {
            return;
        }
        failed = !copied;
        if (copied)
        {
            StateHasChanged();
            await Task.Delay(TimeSpan.FromSeconds(2));
            copied = false;
        }
    }
}
