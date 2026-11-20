using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Noovio.Pulse.Web.Components.Shared;

public partial class ShareButton
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<ShareButton> Logger { get; set; } = default!;

    /// <summary>Fige ce qui est affiché et renvoie l'adresse relative du lien (« partage/… »).</summary>
    [Parameter, EditorRequired] public Func<Task<string>> CreateLink { get; set; } = default!;

    private ShareState state;
    private string? url;

    private enum ShareState
    {
        Idle,
        Creating,
        Copied,

        /// <summary>Lien créé mais presse-papiers indisponible : il est affiché pour être copié à la main.</summary>
        NotCopied,
        Failed
    }

    private async Task ShareAsync()
    {
        state = ShareState.Creating;
        try
        {
            url = Navigation.ToAbsoluteUri(await CreateLink()).AbsoluteUri;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogError(ex, "Création d'un lien de partage impossible");
            state = ShareState.Failed;
            return;
        }

        state = await JS.InvokeAsync<bool>("pulse.copy", url) ? ShareState.Copied : ShareState.NotCopied;
        if (state == ShareState.Copied)
        {
            StateHasChanged();
            await Task.Delay(TimeSpan.FromSeconds(3));
            if (state == ShareState.Copied)
            {
                state = ShareState.Idle;
            }
        }
    }
}
