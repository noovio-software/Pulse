namespace AltPulse.Client.Models;

/// <summary>
/// Un personnage lu dans l'export « alts » de l'addon NoovioPulse : l'état en jeu au moment où il a été relu
/// (connexion, coffre, clé terminée…). Les données du jeu sont plus fraîches et plus complètes que raider.io
/// pour la Grande Chambre forte (raid, monde) et la clé en poche.
/// </summary>
public sealed record AddonCharacter
{
    public required string Region { get; init; }
    public required string RealmSlug { get; init; }
    public required string RealmName { get; init; }
    public required string Name { get; init; }
    public int ClassId { get; init; }
    public int Level { get; init; }
    public string? SpecName { get; init; }
    public string? Role { get; init; }
    public string? RaceName { get; init; }
    public string? Faction { get; init; }
    public double? ItemLevelEquipped { get; init; }
    public double? ItemLevelAverage { get; init; }
    public double Rating { get; init; }
    public string? RatingColor { get; init; }
    public AddonKeystone? Keystone { get; init; }

    /// <summary>Début de la semaine (réinitialisation) où ces données ont été relues.</summary>
    public DateTimeOffset? WeekStartUtc { get; init; }

    /// <summary>Un coffre attend d'être ouvert (récompenses de la semaine passée).</summary>
    public bool HasRewards { get; init; }

    public IReadOnlyList<AddonVaultActivity> Vault { get; init; } = [];
    public IReadOnlyList<AddonRun> Runs { get; init; } = [];
    public DateTimeOffset SavedAtUtc { get; init; }

    public string Key => CharacterKey.Of(Region, RealmSlug, Name);
}

public sealed record AddonKeystone(int MapId, string? MapName, int Level);

/// <summary>Case de la Grande Chambre forte telle que le jeu la donne : type 1 donjons, 3 raid, 6 monde ; niveau = clé ou difficulté.</summary>
public sealed record AddonVaultActivity(int Type, int Index, int Threshold, int Progress, int Level);

public sealed record AddonRun(int MapId, string? MapName, int Level, bool Completed);

public sealed record AddonImportResult(IReadOnlyList<AddonCharacter> Characters, string? Error);

/// <summary>Clé mythique+ en poche.</summary>
public sealed record OwnedKeystone(string ShortName, string Dungeon, int Level);
