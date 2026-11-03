namespace Noovio.FrontPulse.Domain.Entities;

/// <summary>Route MDT recommandée pour un donjon, une plage de niveaux de clé et un jeu d'affixes.</summary>
public sealed class DungeonRoute
{
    public long Id { get; set; }
    public string Season { get; set; } = "";
    public string DungeonSlug { get; set; } = "";
    public string DungeonName { get; set; } = "";
    public int MinKeyLevel { get; set; }
    public int MaxKeyLevel { get; set; }

    /// <summary>Identifiants Blizzard des affixes (voir <see cref="Enums.MythicAffix"/>) ; vide = toutes semaines.</summary>
    public List<int> AffixIds { get; set; } = [];

    public string Title { get; set; } = "";
    public string? Author { get; set; }

    /// <summary>Chaîne d'export Mythic Dungeon Tools, copiée telle quelle dans le jeu.</summary>
    public string MdtString { get; set; } = "";

    public string? SourceUrl { get; set; }
    public bool IsVerified { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
