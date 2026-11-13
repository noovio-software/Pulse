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

    [CascadingParameter] private Task<AuthenticationState> AuthenticationState { get; set; } = default!;

    [Parameter] public long Id { get; set; }

    /// <summary>Spécialisation à comparer : indispensable pour une classe qui en a deux dans le rôle (prêtre).</summary>
    [SupplyParameterFromQuery(Name = "spec")] private string? SpecSlug { get; set; }

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

    private string Anchor(string section) => $"compte/personnages/{Id}/analyse?spec={analysis?.Spec.Slug}#{section}";

    private static string PrimaryStatName(CharacterSheet sheet) =>
        sheet.Intellect >= Math.Max(sheet.Strength, sheet.Agility) ? "Intelligence" : sheet.Strength >= sheet.Agility ? "Force" : "Agilité";

    private sealed record PlanGroup(string Title, string Hint, IReadOnlyList<AnalysisFinding> Findings);

    private IReadOnlyList<AnalysisFinding> Strengths => analysis?.Findings.Where(f => f.Verdict == Verdict.Good).ToList() ?? [];

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
        var bnetId = user.BattleNetId();
        character = bnetId is { } id ? await Accounts.GetCharacterAsync(id, Id, CancellationToken.None) : null;
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
                AnalysisStatus.NoRoleSpec => $"{character.ClassName} n'a pas de spécialisation {Site.Words.Noun}.",
                AnalysisStatus.NoMeta => $"La méta {result.Spec?.SpecName} n'est pas encore calculée : réessaie après la prochaine mise à jour.",
                AnalysisStatus.CharacterNotFound => "Blizzard ne renvoie pas ce personnage (renommé, transféré, ou pas reconnecté depuis la dernière mise à jour du jeu).",
                _ => null
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            failure = ex.GetType().Name;
            error = "Blizzard ou Raider.io ne répond pas pour le moment. Réessaie dans un instant.";
        }

        Benches.Record(BenchEvents.Create(Site, bnetId!.Value, character, result, failure, fromCache, force, Time.GetElapsedTime(started), Time.GetUtcNow()));

        loading = false;
        refreshTooltips = true;
    }
}
