using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.JSInterop;
using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Application.Analysis;
using Noovio.FrontPulse.Domain.Entities;
using Noovio.FrontPulse.Web.Auth;

namespace Noovio.FrontPulse.Web.Components.Pages;

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

    private UserCharacter? character;
    private CharacterAnalysis? analysis;
    private string? error;
    private bool loading = true;
    private bool treeOpen;
    private bool refreshTooltips;

    private IReadOnlyList<AnalysisFinding> ToFix => analysis?.Findings.Where(f => f.Verdict != Verdict.Good).ToList() ?? [];
    /// <summary>Le plan d'action par effort demandé : ce qui se règle ce soir, puis ce qui demande du craft, du farm, des clés.</summary>
    private IReadOnlyList<PlanGroup> PlanGroups =>
    [
        .. new[]
        {
            new PlanGroup("À régler tout de suite", "gemmes, enchantements, talents : sans farmer", ToFix.Where(f => f.Area is AnalysisAreaKind.Stats or AnalysisAreaKind.Gems or AnalysisAreaKind.Enchantments or AnalysisAreaKind.Talents).ToList()),
            new PlanGroup("À crafter", "commandes d'artisanat", ToFix.Where(f => f.Area is AnalysisAreaKind.Embellishments).ToList()),
            new PlanGroup("À farmer", "détail dans la roadmap", ToFix.Where(f => f.Area is AnalysisAreaKind.ItemLevel or AnalysisAreaKind.Trinkets).ToList()),
            new PlanGroup("À jouer", "clés à pousser", ToFix.Where(f => f.Area is AnalysisAreaKind.MythicPlus).ToList())
        }.Where(g => g.Findings.Count > 0)
    ];

    private string Anchor(string section) => $"compte/personnages/{Id}/analyse#{section}";

    private sealed record PlanGroup(string Title, string Hint, IReadOnlyList<AnalysisFinding> Findings);

    private IReadOnlyList<AnalysisFinding> Strengths => analysis?.Findings.Where(f => f.Verdict == Verdict.Good).ToList() ?? [];

    protected override Task OnParametersSetAsync() => LoadAsync(force: false);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (refreshTooltips)
        {
            refreshTooltips = false;
            await JS.InvokeVoidAsync("frontpulse.refreshTooltips");
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

        var key = $"analysis:{character.Id}";
        if (force)
        {
            Cache.Remove(key);
        }

        try
        {
            var result = await Cache.GetOrCreateAsync(key, async entry =>
            {
                var computed = await Analyzer.AnalyzeAsync(character, CancellationToken.None);
                // Un échec (personnage introuvable…) n'est pas gardé en cache : il peut se résoudre au prochain essai.
                entry.AbsoluteExpirationRelativeToNow = computed.Status == AnalysisStatus.Ok ? CacheDuration : TimeSpan.FromSeconds(1);
                return computed;
            });

            analysis = result?.Analysis;
            error = result?.Status switch
            {
                AnalysisStatus.NoTankSpec => $"{character.ClassName} n'a pas de spécialisation tank.",
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
