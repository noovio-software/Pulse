using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Web.Localization;

namespace Noovio.Pulse.Web.ShareCard;

/// <summary>
/// Les vignettes des liens partagés, gardées en mémoire par jeton et par langue. Une analyse partagée ne change jamais :
/// une vignette dessinée reste juste. Elle est préparée dès le partage, car les robots d'aperçu (Facebook / Messenger
/// en tête) passent dans la seconde où le lien est collé et n'attendent pas qu'on aille chercher les images chez Blizzard.
/// </summary>
public sealed class ShareCards(ShareCardRenderer renderer, IMemoryCache cache, IServiceScopeFactory scopes, ILogger<ShareCards> logger)
{
    /// <summary>null si le jeton ne correspond à aucun partage ou si la langue n'est pas servie.</summary>
    public async Task<byte[]?> GetAsync(string token, string language, CancellationToken ct)
    {
        if (!PulseLocalization.Cultures.Contains(language))
        {
            return null;
        }

        var key = $"share-card:{language}:{token}";
        if (cache.TryGetValue(key, out byte[]? cached))
        {
            return cached;
        }

        await using var scope = scopes.CreateAsyncScope();
        if (await scope.ServiceProvider.GetRequiredService<ISharedAnalysisStore>().GetAsync(token, ct) is not { } shared)
        {
            return null;
        }

        // Les textes de la vignette suivent la langue de son adresse, pas celle de la requête.
        var (culture, uiCulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
        try
        {
            var png = await renderer.RenderAsync(shared, ct);
            return cache.Set(key, png, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromHours(12) });
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, uiCulture);
        }
    }

    /// <summary>Dessine la vignette en tâche de fond, sans faire attendre celui qui partage.</summary>
    public void Warm(string token, string language) =>
        _ = Task.Run(async () =>
        {
            try
            {
                await GetAsync(token, language, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Vignette du partage {Token} non préparée", token);
            }
        });
}
