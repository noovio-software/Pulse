using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Import;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Web.Components.Shared;
using Noovio.Pulse.Web.Resources;

namespace Noovio.Pulse.Web.Components.Pages;

/// <summary>
/// Recherche d'un personnage par région, royaume et nom (reprise d'AltPulse), sans connexion.
/// Le personnage est vérifié ici (existence, classe du rôle) pour signaler l'erreur dans le formulaire ;
/// trouvé, il mène à son analyse, sous une adresse stable que l'on peut recharger ou partager.
/// </summary>
public partial class Search
{
    [Inject] private ICharacterSearchSource Characters { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    /// <summary>Région présélectionnée (« ?region=us »), eu par défaut.</summary>
    [SupplyParameterFromQuery(Name = "region")] private string? RegionQuery { get; set; }

    private string region = "eu";
    private string? realm;
    private string? name;
    private IReadOnlyList<RealmInfo> realms = [];
    private string? error;
    private bool searching;

    protected override async Task OnInitializedAsync()
    {
        region = WowRegions.Normalize(RegionQuery);
        realms = await Characters.GetRealmsAsync(region, CancellationToken.None);
    }

    private async Task OnRegionChangedAsync(ChangeEventArgs e)
    {
        region = WowRegions.Normalize(e.Value?.ToString());
        error = null;
        realms = await Characters.GetRealmsAsync(region, CancellationToken.None);
    }

    private async Task SearchAsync()
    {
        error = null;
        var realmSlug = RealmSlug(realm);
        var who = name?.Trim();
        if (realmSlug.Length == 0 || string.IsNullOrEmpty(who))
        {
            error = WebTexts.Search_Required;
            return;
        }

        searching = true;
        try
        {
            var character = await Characters.FindAsync(region, realmSlug, who, CancellationToken.None);
            if (character is null)
            {
                error = Ui.F(WebTexts.Search_NotFound, who, realm!.Trim(), region.ToUpperInvariant());
                return;
            }

            // Une classe sans spécialisation dans le rôle (un mage sur FrontPulse) n'a rien à comparer ici.
            if (Site.Role.ForClass(character.ClassId).Count == 0)
            {
                error = Ui.F(WebTexts.Search_WrongClass, character.Name, ClassName(character.ClassId, character.ClassName), Site.Words.Noun);
                return;
            }

            Navigation.NavigateTo(SearchReport.PathOf(character.Region, character.RealmSlug, character.Name));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            error = WebTexts.Report_Unavailable;
        }
        finally
        {
            searching = false;
        }
    }

    /// <summary>Le royaume choisi dans la liste (par nom ou slug), sinon le texte saisi converti en slug.</summary>
    private string RealmSlug(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "";
        }

        var typed = input.Trim();
        var slug = ImportedCharacterMapper.RealmSlug(typed);
        return realms.FirstOrDefault(r => string.Equals(r.Name, typed, StringComparison.OrdinalIgnoreCase))?.Slug
               ?? realms.FirstOrDefault(r => string.Equals(r.Slug, slug, StringComparison.OrdinalIgnoreCase)
                                             || ImportedCharacterMapper.RealmSlug(r.Name) == slug)?.Slug
               ?? slug;
    }

    /// <summary>Le nom de la classe dans la langue de l'interface, celui de l'API à défaut.</summary>
    internal static string ClassName(int classId, string fallback) =>
        WowClasses.Name(classId) is { Length: > 0 } localized ? localized : fallback;
}
