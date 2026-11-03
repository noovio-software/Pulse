using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Domain.Entities;
using Noovio.FrontPulse.Web.Auth;

namespace Noovio.FrontPulse.Web.Components.Pages;

public partial class AdminIngestion : IAsyncDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(15);

    [Inject] private AdminAccess Access { get; set; } = default!;
    [Inject] private IIngestionRequestStore Requests { get; set; } = default!;

    [CascadingParameter] private Task<AuthenticationState> AuthenticationState { get; set; } = default!;

    private bool isAdmin;
    private bool busy;
    private string? battleTag;
    private string? notice;
    private IReadOnlyList<IngestionRequest> recent = [];
    private PeriodicTimer? timer;

    private bool HasActive => recent.Any(r => r.Status is IngestionRequestStatus.Pending or IngestionRequestStatus.Running);

    protected override async Task OnInitializedAsync()
    {
        var user = (await AuthenticationState).User;
        isAdmin = Access.IsAdmin(user);
        battleTag = user.Identity?.Name;
        if (isAdmin)
        {
            await LoadAsync();
        }
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && isAdmin)
        {
            _ = RefreshLoopAsync();
        }
    }

    private async Task RefreshLoopAsync()
    {
        timer = new PeriodicTimer(RefreshInterval);
        try
        {
            while (await timer.WaitForNextTickAsync())
            {
                await LoadAsync();
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (ObjectDisposedException)
        {
            // Page quittée pendant une actualisation.
        }
    }

    private async Task LoadAsync() => recent = await Requests.GetRecentAsync(10, CancellationToken.None);

    private async Task RequestAsync()
    {
        busy = true;
        try
        {
            var request = await Requests.RequestAsync(battleTag ?? "admin", CancellationToken.None);
            notice = $"Demande n° {request.Id} enregistrée : le worker la lance sous 30 s.";
            await LoadAsync();
        }
        finally
        {
            busy = false;
        }
    }

    private static string StatusLabel(IngestionRequestStatus status) => status switch
    {
        IngestionRequestStatus.Pending => "En attente",
        IngestionRequestStatus.Running => "En cours",
        IngestionRequestStatus.Succeeded => "Terminée",
        _ => "En échec"
    };

    public ValueTask DisposeAsync()
    {
        timer?.Dispose();
        return ValueTask.CompletedTask;
    }
}
