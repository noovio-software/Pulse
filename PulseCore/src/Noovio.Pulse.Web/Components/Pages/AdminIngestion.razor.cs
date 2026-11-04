using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Web.Auth;

namespace Noovio.Pulse.Web.Components.Pages;

public partial class AdminIngestion : IAsyncDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);

    /// <summary>Le worker rend compte toutes les 5 s : au-delà d'une minute sans nouvelles, il est arrêté ou bloqué.</summary>
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(1);

    /// <summary>En dessous, le rythme de lecture des profils est trop bruité pour en tirer une estimation.</summary>
    private const int MinProfilesForRate = 20;

    /// <summary>Requêtes (région, donjon) : une ou deux suffisent, leurs durées sont homogènes.</summary>
    private const int MinQueriesForRate = 2;

    [Inject] private AdminAccess Access { get; set; } = default!;
    [Inject] private IIngestionRequestStore Requests { get; set; } = default!;
    [Inject] private IIngestionRunStore Runs { get; set; } = default!;
    [Inject] private RefreshSchedule Schedule { get; set; } = default!;

    [CascadingParameter] private Task<AuthenticationState> AuthenticationState { get; set; } = default!;

    private bool isAdmin;
    private bool busy;
    private string? battleTag;
    private string? notice;
    private IReadOnlyList<IngestionRequest> recent = [];
    private IReadOnlyList<IngestionRun> runs = [];
    private DateTimeOffset? nextScheduled;
    private PeriodicTimer? timer;

    private bool HasActive => recent.Any(r => r.Status is IngestionRequestStatus.Pending or IngestionRequestStatus.Running);

    /// <summary>L'exécution en cours, sinon la dernière : le panneau d'avancement montre l'une ou l'autre.</summary>
    private IngestionRun? Latest => runs.FirstOrDefault();

    /// <summary>Dernière ingestion réussie : ses durées servent d'estimation tant que le rythme en cours n'est pas mesurable.</summary>
    private IngestionRun? LastSucceeded =>
        runs.FirstOrDefault(r => r.Status == IngestionRunStatus.Succeeded && r.CompletedAtUtc is not null && r.ProfilesStartedAtUtc is not null);

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

    private async Task LoadAsync()
    {
        recent = await Requests.GetRecentAsync(10, CancellationToken.None);
        runs = await Runs.GetRecentAsync(10, CancellationToken.None);
        nextScheduled = await Schedule.GetNextAsync();
    }

    private async Task RequestAsync()
    {
        busy = true;
        try
        {
            var request = await Requests.RequestAsync(battleTag ?? "admin", CancellationToken.None);
            notice = Latest?.Status == IngestionRunStatus.Running
                ? $"Demande n° {request.Id} rattachée à l'ingestion en cours : elle passe « en cours » sous 5 s."
                : $"Demande n° {request.Id} enregistrée : le worker la lance sous 5 s.";
            await LoadAsync();
        }
        finally
        {
            busy = false;
        }
    }

    /// <summary>Fin de la lecture du classement, au rythme des requêtes déjà parcourues ; null tant qu'il n'est pas mesurable.</summary>
    private static TimeSpan? LeaderboardRemaining(IngestionRun run, DateTimeOffset now) =>
        run.Phase == IngestionPhase.Leaderboard
            ? AtRate(run.StartedAtUtc, run.LeaderboardQueriesDone, run.LeaderboardQueries, MinQueriesForRate, run, now)
            : TimeSpan.Zero;

    /// <summary>
    /// Temps restant de toute l'exécution. Pendant le classement : son rythme, plus la durée des profils de la dernière
    /// ingestion réussie. Pendant les profils : leur rythme, à défaut la dernière ingestion. Null quand rien ne permet de l'estimer.
    /// </summary>
    private TimeSpan? Remaining(IngestionRun run, DateTimeOffset now)
    {
        var last = LastSucceeded;
        var lastProfiles = last is null ? (TimeSpan?)null : last.CompletedAtUtc!.Value - last.ProfilesStartedAtUtc!.Value;

        var remaining = run.Phase == IngestionPhase.Leaderboard
            ? LeaderboardRemaining(run, now) + lastProfiles
              ?? (last is null ? null : last.CompletedAtUtc!.Value - last.StartedAtUtc - (now - run.StartedAtUtc))
            : AtRate(run.ProfilesStartedAtUtc!.Value, run.ProfilesRead, run.Candidates, MinProfilesForRate, run, now)
              ?? lastProfiles - (now - run.ProfilesStartedAtUtc!.Value);

        return remaining is { } r && r < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    /// <summary>Ce qui reste à faire au rythme constaté depuis <paramref name="from"/> jusqu'au dernier compte rendu du worker.</summary>
    private static TimeSpan? AtRate(DateTimeOffset from, int done, int total, int minDone, IngestionRun run, DateTimeOffset now) =>
        done < minDone || total == 0
            ? null
            : (run.UpdatedAtUtc - from) / done * Math.Max(0, total - done) - (now - run.UpdatedAtUtc);

    /// <summary>Part du temps écoulé sur la durée estimée ; null tant que rien ne permet de l'évaluer (barre en balayage).</summary>
    private static double? Fraction(IngestionRun run, TimeSpan? remaining, DateTimeOffset now)
    {
        if (remaining is not { } r)
        {
            return null;
        }

        var elapsed = now - run.StartedAtUtc;
        return Math.Clamp(elapsed / (elapsed + r), 0, 0.99);
    }

    private static double StepFraction(int done, int total) => total == 0 ? 0 : Math.Clamp((double)done / total, 0, 1);

    /// <summary>Les spécialisations sont traitées dans l'ordre de <c>Site.Role.Specs</c>.</summary>
    private static string? SpecState(IngestionRun run, SpecInfo spec, int index) =>
        run.Status == IngestionRunStatus.Succeeded || index < run.SpecsDone ? "is-done"
            : run.Status == IngestionRunStatus.Running && run.Phase == IngestionPhase.Profiles && run.CurrentSpec == spec.Spec ? "is-current"
            : null;

    private static string ParisTime(DateTimeOffset at) => RefreshSchedule.ToParis(at).ToString("HH'h'mm");

    private static string StatusLabel(IngestionRequestStatus status) => status switch
    {
        IngestionRequestStatus.Pending => "En attente",
        IngestionRequestStatus.Running => "En cours",
        IngestionRequestStatus.Succeeded => "Terminée",
        _ => "En échec"
    };

    private static string RunStatusLabel(IngestionRunStatus status) => status switch
    {
        IngestionRunStatus.Running => "En cours",
        IngestionRunStatus.Succeeded => "Terminée",
        _ => "En échec"
    };

    public ValueTask DisposeAsync()
    {
        timer?.Dispose();
        return ValueTask.CompletedTask;
    }
}
