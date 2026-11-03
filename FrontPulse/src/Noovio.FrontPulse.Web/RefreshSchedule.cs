using Microsoft.Extensions.Options;
using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Application.Ingestion;

namespace Noovio.FrontPulse.Web;

/// <summary>
/// Heure estimée du prochain rechargement des données, affichée dans l'en-tête. Le calendrier vit dans le worker :
/// le site la déduit du snapshot le plus récent plus l'intervalle d'ingestion (le worker repart de la fin de l'exécution).
/// </summary>
public sealed class RefreshSchedule(IServiceScopeFactory scopes, IOptions<IngestionOptions> options)
{
    /// <summary>Heure de Paris : le site est en français, le serveur peut tourner en UTC.</summary>
    private static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

    /// <summary>
    /// Prochain rechargement, ou null avant la première ingestion. Un scope à part : l'en-tête se rend
    /// en même temps que la page, qui lit elle aussi les snapshots (un DbContext ne supporte pas deux requêtes à la fois).
    /// </summary>
    public async Task<DateTimeOffset?> GetNextAsync(CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var summaries = await scope.ServiceProvider.GetRequiredService<ISpecMetaQueries>().GetSummariesAsync(ct);
        return summaries.Count == 0 ? null : summaries.Max(s => s.ComputedAtUtc) + options.Value.Interval;
    }

    public static DateTimeOffset ToParis(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, Paris);
}
