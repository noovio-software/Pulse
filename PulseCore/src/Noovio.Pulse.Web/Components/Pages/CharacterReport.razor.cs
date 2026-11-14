using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.JSInterop;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Analysis;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Web.Auth;

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

        var user = (await AuthenticationState).User;
        character = user.BattleNetId() is { } bnetId ? await Accounts.GetCharacterAsync(bnetId, Id, CancellationToken.None) : null;
        if (character is null)
        {
            // Inexistant ou appartenant à un autre compte : même réponse, pour ne rien divulguer.
            error = "Personnage introuvable dans ton compte. Synchronise tes personnages puis réessaie.";
            loading = false;
            return;
        }

        var requested = Site.Role.FromSlug(SpecSlug);
        var key = $"analysis:{character.Id}:{requested?.Slug}";
        if (force)
        {
            Cache.Remove(key);
        }

        try
        {
            var result = await Cache.GetOrCreateAsync(key, async entry =>
            {
                var computed = await Analyzer.AnalyzeAsync(character, requested, CancellationToken.None);
                // Un échec (personnage introuvable…) n'est pas gardé en cache : il peut se résoudre au prochain essai.
                entry.AbsoluteExpirationRelativeToNow = computed.Status == AnalysisStatus.Ok ? CacheDuration : TimeSpan.FromSeconds(1);
                return computed;
            });

            analysis = result?.Analysis;
            error = result?.Status switch
            {
                AnalysisStatus.NoRoleSpec => $"{character.ClassName} n'a pas de spécialisation {Site.Words.Noun}.",
                AnalysisStatus.NoMeta => $"La méta {result.Spec?.SpecName} n'est pas encore calculée : réessaie après la prochaine mise à jour.",
                AnalysisStatus.CharacterNotFound => "Blizzard ne renvoie pas ce personnage (renommé, transféré, ou pas reconnecté depuis la dernière mise à jour du jeu).",
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
