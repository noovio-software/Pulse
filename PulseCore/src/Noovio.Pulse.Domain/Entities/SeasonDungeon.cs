using System.Text.Json.Serialization;

namespace Noovio.Pulse.Domain.Entities;

/// <summary>Un donjon de la saison et ses routes populaires, relues d'un bloc : une ligne par (saison, donjon).</summary>
public sealed class SeasonDungeon
{
    public long Id { get; set; }
    public string Season { get; set; } = "";

    /// <summary>Slug Keystone.guru, repris dans l'adresse de la page du donjon.</summary>
    public string Slug { get; set; } = "";

    public string Name { get; set; } = "";
    public string ShortName { get; set; } = "";
    public int TimerSeconds { get; set; }
    public string? IconUrl { get; set; }
    public string? ImageUrl { get; set; }

    /// <summary>Par ordre de popularité sur Keystone.guru.</summary>
    public List<DungeonRoute> Routes { get; set; } = [];

    public DateTimeOffset FetchedAtUtc { get; set; }
}

/// <summary>Une route Keystone.guru, réduite à ce que le site affiche et à ce qui intéresse le tank : la taille de chaque pull.</summary>
/// <param name="GameSeason">Numéro de saison Keystone.guru (affixes de la route) : sert à écarter les routes d'une saison passée.</param>
/// <param name="PullSizes">Nombre d'ennemis de chaque pull, dans l'ordre de la route.</param>
public sealed record DungeonRoute(
    string PublicKey,
    string Title,
    string? Author,
    string? AuthorUrl,
    int EnemyForces,
    int EnemyForcesRequired,
    IReadOnlyList<int> PullSizes,
    string ViewUrl,
    string EmbedUrl,
    string? ThumbnailUrl,
    int? GameSeason,
    DateTimeOffset? ExpiresAtUtc)
{
    [JsonIgnore]
    public int LargestPull => PullSizes.Count == 0 ? 0 : PullSizes.Max();

    /// <summary>Moyenne sur les pulls qui comptent au moins un ennemi.</summary>
    [JsonIgnore]
    public double AveragePull => PullSizes.Any(s => s > 0) ? PullSizes.Where(s => s > 0).Average() : 0;
}
