using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.JSInterop;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Analysis;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Usage;
using Noovio.Pulse.Web.Auth;
using Noovio.Pulse.Web.Components.Shared;
using Noovio.Pulse.Web.Localization;
using Noovio.Pulse.Web.Resources;
using Noovio.Pulse.Web.Usage;

namespace Noovio.Pulse.Web.Components.Pages;

/// <summary>
/// Analyse d'un personnage trouvé par recherche (région, royaume, nom), sans connexion. Même traitement qu'un
/// personnage du compte : sans prérendu, l'analyse interroge Blizzard et Raider.io ; le résultat est gardé 10 minutes.
/// L'adresse ne porte que la région, le royaume et le nom : elle se recharge et se partage.
/// </summary>
public partial class SearchReport
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    [Inject] private ICharacterSearchSource Characters { get; set; } = default!;
    [Inject] private CharacterAnalysisService Analyzer { get; set; } = default!;
    [Inject] private IMemoryCache Cache { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IUsageRecorder Benches { get; set; } = default!;
    [Inject] private TimeProvider Time { get; set; } = default!;

    /// <summary>La recherche se fait sans connexion : le compte n'est noté que si le visiteur est connecté.</summary>
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationState { get; set; }

    [Parameter] public string Region { get; set; } = "";
    [Parameter] public string Realm { get; set; } = "";
    [Parameter] public string Name { get; set; } = "";

    /// <summary>Spécialisation à comparer : pour une classe qui en a plusieurs dans le rôle.</summary>
    [SupplyParameterFromQuery(Name = "spec")] private string? SpecSlug { get; set; }

    private UserCharacter? character;
    private CharacterAnalysis? analysis;
    private string? error;
    private bool loading = true;
    private bool refreshTooltips;

    private string SearchUrl => $"rechercher?region={WowRegions.Normalize(Region)}";

    /// <summary>Adresse de l'analyse d'un personnage, sans paramètre.</summary>
    public static string PathOf(string region, string realmSlug, string name) =>
        $"personnages/{WowRegions.Normalize(region)}/{Uri.EscapeDataString(realmSlug.ToLowerInvariant())}/{Uri.EscapeDataString(name)}";

    protected override Task OnParametersSetAsync() => LoadAsync(force: false);

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
        analysis = null;

        var region = WowRegions.Normalize(Region);
        var started = Time.GetTimestamp();
        var fromCache = true;
        AnalysisResult? result = null;
        string? failure = null;
        try
        {
            character = await Characters.FindAsync(region, Realm, Name, CancellationToken.None);
            if (character is null)
            {
                error = Ui.F(WebTexts.Search_NotFound, Name, Realm, region.ToUpperInvariant());
                result = new AnalysisResult(AnalysisStatus.CharacterNotFound, null);
            }
            else if (Site.Role.ForClass(character.ClassId).Count == 0)
            {
                // Lien saisi à la main, ou partagé depuis le site d'un autre rôle : pas d'analyse pour une classe hors du rôle.
                error = Ui.F(WebTexts.Search_WrongClass, character.Name, Search.ClassName(character.ClassId, character.ClassName), Site.Words.Noun);
                result = new AnalysisResult(AnalysisStatus.NoRoleSpec, null);
            }
            else
            {
                var requested = Site.Role.FromSlug(SpecSlug);
                // Les textes de l'analyse sont rédigés dans la langue du visiteur : une entrée par langue.
                var key = $"search-analysis:{PulseLocalization.CurrentLanguage}:{character.Region}:{character.RealmSlug}:{character.Name.ToLowerInvariant()}:{requested?.Slug}";
                if (force)
                {
                    Cache.Remove(key);
                }

                result = await Cache.GetOrCreateAsync(key, async entry =>
                {
                    fromCache = false;
                    var computed = await Analyzer.AnalyzeAsync(character, requested, CancellationToken.None);
                    // Un échec n'est pas gardé en cache : il peut se résoudre au prochain essai.
                    entry.AbsoluteExpirationRelativeToNow = computed.Status == AnalysisStatus.Ok ? CacheDuration : TimeSpan.FromSeconds(1);
                    return computed;
                });

                analysis = result?.Analysis;
                error = result?.Status switch
                {
                    AnalysisStatus.NoRoleSpec => Ui.F(WebTexts.Search_WrongClass, character.Name, Search.ClassName(character.ClassId, character.ClassName), Site.Words.Noun),
                    AnalysisStatus.NoMeta => Ui.F(WebTexts.Report_NoMeta, result.Spec?.SpecName),
                    AnalysisStatus.CharacterNotFound => WebTexts.Report_CharacterNotFound,
                    _ => null
                };
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            failure = ex.GetType().Name;
            error = WebTexts.Report_Unavailable;
        }

        var account = AuthenticationState is null ? null : (await AuthenticationState).User.BattleNetId();
        var benched = character ?? new UserCharacter { Region = region, RealmSlug = Cut(Realm), RealmName = Cut(Realm), Name = Cut(Name) };
        Benches.Record(BenchEvents.CreateSearch(Site, account, benched, result, failure, fromCache, force, Time.GetElapsedTime(started), Time.GetUtcNow()));

        loading = false;
        refreshTooltips = true;
    }

    /// <summary>Les segments de l'adresse viennent du visiteur : coupés à la taille des colonnes du bench.</summary>
    private static string Cut(string text) => text.Length <= 64 ? text : text[..64];
}
