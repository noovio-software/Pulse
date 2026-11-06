using Noovio.Pulse.Domain.Entities;

namespace Noovio.Pulse.Application.Abstractions;

/// <summary>Les donjons de la saison en cours (Raider.io).</summary>
public interface ISeasonDungeonSource
{
    Task<SeasonDungeonList> GetCurrentDungeonsAsync(CancellationToken ct);
}

/// <summary>Les routes publiées sur Keystone.guru.</summary>
public interface IDungeonRouteSource
{
    /// <summary>Slug Keystone.guru de chaque donjon, par identifiant de mode défi (le même que chez Raider.io et Blizzard).</summary>
    Task<IReadOnlyDictionary<int, string>> GetDungeonSlugsAsync(CancellationToken ct);

    /// <summary>Clés des routes populaires d'un donjon, de la plus populaire à la moins populaire.</summary>
    Task<IReadOnlyList<string>> GetPopularRouteKeysAsync(string dungeonSlug, int count, CancellationToken ct);

    /// <summary>null si la route n'existe plus ou n'est plus publique.</summary>
    Task<DungeonRoute?> GetRouteAsync(string publicKey, CancellationToken ct);
}

/// <summary>Écriture des routes par le worker.</summary>
public interface IDungeonRouteStore
{
    /// <summary>Remplace, en une transaction, le donjon de cette saison et ses routes.</summary>
    Task ReplaceAsync(SeasonDungeon dungeon, CancellationToken ct);

    /// <summary>Date de la dernière synchronisation, pour ne pas tout relire à chaque redémarrage du worker.</summary>
    Task<DateTimeOffset?> GetLastFetchedAsync(CancellationToken ct);
}

/// <summary>Lecture des routes par le site.</summary>
public interface IDungeonRouteQueries
{
    /// <summary>Les donjons de la dernière saison synchronisée, par ordre alphabétique ; vide avant la première synchronisation.</summary>
    Task<IReadOnlyList<SeasonDungeon>> GetCurrentAsync(CancellationToken ct = default);
}

public sealed record SeasonDungeonList(string Season, IReadOnlyList<SeasonDungeonInfo> Dungeons);

/// <param name="Name">Nom anglais, celui de Raider.io.</param>
public sealed record SeasonDungeonInfo(
    string Slug,
    string Name,
    string ShortName,
    int ChallengeModeId,
    int TimerSeconds,
    string? IconUrl,
    string? ImageUrl);
