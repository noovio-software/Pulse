using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.JSInterop;
using Noovio.Pulse.Application.Analysis;
using Noovio.Pulse.Application.Import;
using Noovio.Pulse.Web.Components.Shared;

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

    [Parameter] public string Key { get; set; } = "";

    /// <summary>Spécialisation à comparer : pour une classe qui en a plusieurs dans le rôle.</summary>
    [SupplyParameterFromQuery(Name = "spec")] private string? SpecSlug { get; set; }

    private CharacterImport? import;
    private CharacterAnalysis? analysis;
    private string? error;
    private bool loading = true;
    private bool refreshTooltips;

    private string SourceLabel => import?.ExportedAtUtc is { } at ? $"Addon · {Ui.Ago(at)}" : "Addon";

    private string SourceTitle =>
        $"Lu en jeu par l'addon NoovioPulse{(import?.Addon is { Length: > 0 } v ? $" {v}" : "")}{(import?.Game is { Length: > 0 } g ? $", client {g}" : "")}";

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
                ? $"Import introuvable : il a expiré (les imports sont gardés {AddonInfo.Retention.TotalDays} jours) ou le lien est incomplet."
                : parsed.Error;
            loading = false;
            return;
        }

        import = parsed.Import!;
        var requested = Site.Role.FromSlug(SpecSlug);
        var key = $"import-analysis:{Key}:{requested?.Slug}";
        if (force)
        {
            Cache.Remove(key);
        }

        try
        {
            var result = await Cache.GetOrCreateAsync(key, async entry =>
            {
                var computed = await Analyzer.AnalyzeImportAsync(import, requested, CancellationToken.None);
                entry.AbsoluteExpirationRelativeToNow = computed.Status == AnalysisStatus.Ok ? CacheDuration : TimeSpan.FromSeconds(1);
                return computed;
            });

            analysis = result?.Analysis;
            error = result?.Status switch
            {
                AnalysisStatus.NoRoleSpec => $"Cette classe n'a pas de spécialisation {Site.Words.Noun} : importe ce personnage sur le site de son rôle.",
                AnalysisStatus.NoMeta => $"La méta {result.Spec?.SpecName} n'est pas encore calculée : réessaie après la prochaine mise à jour.",
                _ => null
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            error = "Blizzard ou Raider.io ne répond pas pour le moment. Réessaie dans un instant.";
        }

        loading = false;
        refreshTooltips = true;
    }
}
