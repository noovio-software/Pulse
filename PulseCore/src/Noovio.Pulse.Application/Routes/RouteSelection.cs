using System.Text.RegularExpressions;
using Noovio.Pulse.Domain.Entities;

namespace Noovio.Pulse.Application.Routes;

/// <summary>Ce qui distingue une route dans la liste d'un donjon, pour le tank qui la choisit.</summary>
public enum RouteHighlight
{
    /// <summary>La première de la liste populaire de Keystone.guru.</summary>
    MostPopular,

    /// <summary>L'auteur ou le titre se présente comme tank : c'est du texte libre, pas une donnée vérifiée.</summary>
    TankAuthored,

    /// <summary>La plus grande moyenne d'ennemis par pull.</summary>
    BiggestPulls,

    /// <summary>Le plus gros pull le moins gros de la liste.</summary>
    SmallestPeak
}

public static partial class RouteSelection
{
    /// <summary>
    /// Garde, dans l'ordre de popularité, les routes jouables telles quelles : ni expirées, ni vides, ni incomplètes.
    /// Les donjons repris d'une ancienne extension ont aussi des routes de saisons passées : seule la saison la plus
    /// récente de la liste est retenue.
    /// </summary>
    public static IReadOnlyList<DungeonRoute> Keep(IEnumerable<DungeonRoute> routes, DateTimeOffset now, int count)
    {
        var usable = routes
            .Where(r => r.ExpiresAtUtc is null || r.ExpiresAtUtc > now)
            .Where(r => r.PullSizes.Count > 0 && r.EnemyForces >= r.EnemyForcesRequired)
            .ToList();
        var latestSeason = usable.Max(r => r.GameSeason);

        return usable
            .Where(r => latestSeason is null || r.GameSeason == latestSeason)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Les distinctions de chaque route, par clé. Celles qui comparent les pulls ne sont données que si
    /// les routes diffèrent vraiment : à valeurs égales, aucune n'est plus remarquable qu'une autre.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<RouteHighlight>> Highlights(IReadOnlyList<DungeonRoute> routes)
    {
        var highlights = routes.ToDictionary(r => r.PublicKey, _ => new List<RouteHighlight>());
        if (routes.Count == 0)
        {
            return highlights.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<RouteHighlight>)kv.Value);
        }

        highlights[routes[0].PublicKey].Add(RouteHighlight.MostPopular);
        foreach (var route in routes.Where(r => TankWord().IsMatch($"{r.Author} {r.Title}")))
        {
            highlights[route.PublicKey].Add(RouteHighlight.TankAuthored);
        }

        if (routes.Select(r => r.AveragePull).Distinct().Count() > 1)
        {
            highlights[routes.MaxBy(r => r.AveragePull)!.PublicKey].Add(RouteHighlight.BiggestPulls);
        }

        if (routes.Select(r => r.LargestPull).Distinct().Count() > 1)
        {
            highlights[routes.MinBy(r => r.LargestPull)!.PublicKey].Add(RouteHighlight.SmallestPeak);
        }

        return highlights.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<RouteHighlight>)kv.Value);
    }

    [GeneratedRegex(@"\btank\b", RegexOptions.IgnoreCase)]
    private static partial Regex TankWord();
}
