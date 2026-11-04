using Microsoft.AspNetCore.Components;

namespace Noovio.Pulse.Web.Components.Shared;

public partial class RedirectToLogin
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    protected override void OnInitialized() =>
        Navigation.NavigateTo(
            $"compte/connexion?returnUrl={Uri.EscapeDataString("/" + Navigation.ToBaseRelativePath(Navigation.Uri))}",
            forceLoad: true);
}
