using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.JSInterop;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Analysis;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Usage;
using Noovio.Pulse.Web.Auth;
using Noovio.Pulse.Web.Usage;
using Noovio.Pulse.Web.Components.Shared;
using Noovio.Pulse.Web.Localization;
using Noovio.Pulse.Web.Resources;
using Noovio.Pulse.Web.ShareCard;

namespace Noovio.Pulse.Web.Components.Pages;

/// <summary>
/// Analyse d'un personnage du compte. Rendue sans prérendu : l'analyse interroge Blizzard et Raider.io
/// (plusieurs secondes), et ne doit tourner qu'une fois ; le résultat est gardé 10 minutes.
/// </summary>
public partial class CharacterReport
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    [Inject] private IAccountStore Accounts { get; set; } = default!;
    [Inject] private CharacterAnalysisService Analyzer { get; set; } = default!;
    [Inject] private IMemoryCache Cache { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IUsageRecorder Benches { get; set; } = default!;
    [Inject] private TimeProvider Time { get; set; } = default!;
    [Inject] private ISharedAnalysisStore SharedAnalyses { get; set; } = default!;
    [Inject] private ShareCards ShareCards { get; set; } = default!;

    [CascadingParameter] private Task<AuthenticationState> AuthenticationState { get; set; } = default!;

    [Parameter] public long Id { get; set; }

    /// <summary>Spécialisation à comparer : indispensable pour une classe qui en a deux dans le rôle (prêtre).</summary>
    [SupplyParameterFromQuery(Name = "spec")] private string? SpecSlug { get; set; }

    private UserCharacter? character;
    private CharacterAnalysis? analysis;
    private string? error;
    private bool loading = true;
    private bool refreshTooltips;

    protected override Task OnParametersSetAsync() => LoadAsync(force: false);

    /// <summary>
    /// Fige l'analyse affichée (l'objet même qui est à l'écran, méta comprise) et renvoie l'adresse de son lien.
    /// Relancer l'analyse en produit une nouvelle : le lien déjà donné ne bouge plus.
    /// </summary>
    private async Task<string> ShareAsync(CharacterAnalysis shown)
    {
        var bnetId = (await AuthenticationState).User.BattleNetId()
                     ?? throw new InvalidOperationException("Partage sans compte Battle.net");
        var token = await SharedAnalyses.ShareAsync(bnetId, character!, shown, CancellationToken.None);
        // Le robot d'aperçu passe dès que le lien est collé, dans une langue qu'on ne connaît pas : les deux vignettes sont prêtes.
        foreach (var language in PulseLocalization.Cultures)
        {
            ShareCards.Warm(token, language);
        }
        return $"partage/{token}";
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (refreshTooltips)
        {
            refreshTooltips = false;
            try
            {
                await JS.InvokeVoidAsync("pulse.refreshTooltips");
            }
            catch (JSDisconnectedException)
            {
                // Circuit fermé (onglet quitté) : plus rien à mettre à jour.
            }
        }
    }

    private async Task LoadAsync(bool force)
    {
        loading = true;
        error = null;

        var user = (await AuthenticationState).User;
        var bnetId = user.BattleNetId();
        character = bnetId is { } id ? await Accounts.GetCharacterAsync(id, Id, CancellationToken.None) : null;
        if (character is null)
        {
            // Inexistant ou appartenant à un autre compte : même réponse, pour ne rien divulguer.
            error = WebTexts.Report_CharacterMissing;
            loading = false;
            return;
        }

        var requested = Site.Role.FromSlug(SpecSlug);
        // Les textes de l'analyse sont rédigés dans la langue du visiteur : une entrée par langue.
        var key = $"analysis:{PulseLocalization.CurrentLanguage}:{character.Id}:{requested?.Slug}";
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
                var computed = await Analyzer.AnalyzeAsync(character, requested, CancellationToken.None);
                // Un échec (personnage introuvable…) n'est pas gardé en cache : il peut se résoudre au prochain essai.
                entry.AbsoluteExpirationRelativeToNow = computed.Status == AnalysisStatus.Ok ? CacheDuration : TimeSpan.FromSeconds(1);
                return computed;
            });

            analysis = result?.Analysis;
            error = result?.Status switch
            {
                AnalysisStatus.NoRoleSpec => Ui.F(WebTexts.Report_NoRoleSpec, WowClasses.Name(character.ClassId) is { Length: > 0 } name ? name : character.ClassName, Site.Words.Noun),
                AnalysisStatus.NoMeta => Ui.F(WebTexts.Report_NoMeta, result.Spec?.SpecName),
                AnalysisStatus.CharacterNotFound => WebTexts.Report_CharacterNotFound,
                _ => null
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            failure = ex.GetType().Name;
            error = WebTexts.Report_Unavailable;
        }

        Benches.Record(BenchEvents.Create(Site, bnetId!.Value, character, result, failure, fromCache, force, Time.GetElapsedTime(started), Time.GetUtcNow()));

        loading = false;
        refreshTooltips = true;
    }
}
