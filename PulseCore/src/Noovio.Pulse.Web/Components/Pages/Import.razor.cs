using System.Security.Cryptography;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Caching.Distributed;
using Noovio.Pulse.Application.Import;
using Noovio.Pulse.Web.Components.Shared;
using Noovio.Pulse.Web.Resources;

namespace Noovio.Pulse.Web.Components.Pages;

/// <summary>
/// Collage de la chaîne de l'addon, en rendu statique (formulaire classique : une chaîne de plusieurs dizaines de Ko
/// passerait mal par le circuit interactif). Une chaîne valide est gardée sous une clé aléatoire, et l'utilisateur est
/// renvoyé vers son analyse : un lien stable, que l'on peut recharger ou partager.
/// </summary>
public partial class Import
{
    [Inject] private IDistributedCache Cache { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [SupplyParameterFromForm(FormName = "import")] private ImportInput? Input { get; set; }

    private string? error;

    /// <summary>
    /// « Depuis CurseForge, ou en téléchargement direct : dépose alors le dossier NoovioPulse dans _retail_\Interface\AddOns. »,
    /// avec les deux liens et les noms de dossiers en code.
    /// </summary>
    private static MarkupString Step1Text => new(Ui.F(
        System.Net.WebUtility.HtmlEncode(WebTexts.Import_Step1Text),
        $"""<a href="{AddonInfo.CurseForgeUrl}" target="_blank" rel="noopener">CurseForge</a>""",
        $"""<a href="{AddonInfo.DirectDownloadUrl}" target="_blank" rel="noopener">{System.Net.WebUtility.HtmlEncode(WebTexts.Import_DirectDownload)}</a>""",
        "<code>NoovioPulse</code>", @"<code>_retail_\Interface\AddOns</code>"));

    protected override void OnInitialized() => Input ??= new();

    private async Task SubmitAsync()
    {
        var result = CharacterImportParser.Parse(Input!.Text);
        if (!result.Succeeded)
        {
            error = result.Error;
            return;
        }

        var key = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(12));
        await Cache.SetStringAsync(AddonInfo.CacheKey(key), Input.Text!,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = AddonInfo.Retention });

        Navigation.NavigateTo($"importer/{key}");
    }

    public sealed class ImportInput
    {
        public string? Text { get; set; }
    }
}
