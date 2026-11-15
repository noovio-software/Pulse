using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.JSInterop;
using Noovio.Pulse.Application.Analysis;
using Noovio.Pulse.Application.Import;
using Noovio.Pulse.Usage;
using Noovio.Pulse.Web.Auth;
using Noovio.Pulse.Web.Components.Shared;
using Noovio.Pulse.Web.Localization;
using Noovio.Pulse.Web.Resources;
using Noovio.Pulse.Web.Usage;

namespace Noovio.Pulse.Web.Components.Pages;

/// <summary>
/// Analyse d'un export de l'addon, retrouvé par sa clé. Sans prérendu, comme l'analyse d'un personnage du compte :
/// elle interroge Raider.io et ne doit tourner qu'une fois ; le résultat est gardé 10 minutes.
/// </summary>
public partial class ImportReport
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    [Inject] private IDistributedCache Imports { get; set; } = default!;
    [Inject] private CharacterAnalysisService Analyzer { get; set; } = default!;
    [Inject] private IMemoryCache Cache { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IUsageRecorder Benches { get; set; } = default!;
    [Inject] private TimeProvider Time { get; set; } = default!;

    /// <summary>L'import se fait sans connexion : le compte n'est noté que si le visiteur est connecté.</summary>
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationState { get; set; }

    [Parameter] public string Key { get; set; } = "";

    /// <summary>Spécialisation à comparer : pour une classe qui en a plusieurs dans le rôle.</summary>
    [SupplyParameterFromQuery(Name = "spec")] private string? SpecSlug { get; set; }

    private CharacterImport? import;
    private CharacterAnalysis? analysis;
    private string? error;
    private bool loading = true;
    private bool refreshTooltips;

    private string SourceLabel => import?.ExportedAtUtc is { } at ? Ui.F(WebTexts.Report_SourceAddonAgo, Ui.Ago(at)) : WebTexts.Report_SourceAddon;

    private string SourceTitle => Ui.F(WebTexts.Report_SourceTitle,
        import?.Addon is { Length: > 0 } v ? $" {v}" : "",
        import?.Game is { Length: > 0 } g ? Ui.F(WebTexts.Report_SourceClient, g) : "");

    protected override Task OnParametersSetAsync() => LoadAsync(force: false);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (refreshTooltips)
        {
            refreshTooltips = false;
            await JS.InvokeVoidAsync("pulse.refreshTooltips");
        }
    }

    private async Task LoadAsync(bool force)
    {
        loading = true;
        error = null;

        var raw = await Imports.GetStringAsync(AddonInfo.CacheKey(Key));
        var parsed = CharacterImportParser.Parse(raw);
        if (raw is null || !parsed.Succeeded)
        {
            error = raw is null
                ? Ui.F(WebTexts.Report_ImportExpired, AddonInfo.Retention.TotalDays)
                : parsed.Error;
            loading = false;
            return;
        }

        import = parsed.Import!;
        var requested = Site.Role.FromSlug(SpecSlug);
        // Les textes de l'analyse sont rédigés dans la langue du visiteur : une entrée par langue.
        var key = $"import-analysis:{PulseLocalization.CurrentLanguage}:{Key}:{requested?.Slug}";
        if (force)
        {
            Cache.Remove(key);
        }

        var started = Time.GetTimestamp();
        var fromCache = true;
        AnalysisResult? result = null;
        string? failure = null;
        try
        {
            result = await Cache.GetOrCreateAsync(key, async entry =>
            {
                fromCache = false;
                var computed = await Analyzer.AnalyzeImportAsync(import, requested, CancellationToken.None);
                entry.AbsoluteExpirationRelativeToNow = computed.Status == AnalysisStatus.Ok ? CacheDuration : TimeSpan.FromSeconds(1);
                return computed;
            });

            analysis = result?.Analysis;
            error = result?.Status switch
            {
                AnalysisStatus.NoRoleSpec => Ui.F(WebTexts.Report_ImportNoRoleSpec, Site.Words.Noun),
                AnalysisStatus.NoMeta => Ui.F(WebTexts.Report_NoMeta, result.Spec?.SpecName),
                _ => null
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            failure = ex.GetType().Name;
            error = WebTexts.Report_Unavailable;
        }

        var account = AuthenticationState is null ? null : (await AuthenticationState).User.BattleNetId();
        Benches.Record(BenchEvents.CreateImport(Site, account, import, result, failure, fromCache, force, Time.GetElapsedTime(started), Time.GetUtcNow()));

        loading = false;
        refreshTooltips = true;
    }
}
