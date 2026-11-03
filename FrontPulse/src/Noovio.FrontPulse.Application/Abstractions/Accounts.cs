using Noovio.FrontPulse.Domain.Entities;
using Noovio.FrontPulse.Domain.Enums;

namespace Noovio.FrontPulse.Application.Abstractions;

public interface IAccountStore
{
    /// <summary>Crée le compte à la première connexion, sinon met à jour BattleTag et date de connexion.</summary>
    Task<UserAccount> UpsertAsync(long battleNetId, string battleTag, CancellationToken ct);

    /// <summary>Remplace la liste des personnages (les personnages supprimés ou transférés disparaissent).</summary>
    Task ReplaceCharactersAsync(long accountId, IReadOnlyList<UserCharacter> characters, CancellationToken ct);

    Task<UserAccount?> GetAsync(long battleNetId, CancellationToken ct);

    /// <summary>Un personnage, seulement s'il appartient au compte (pas d'accès aux personnages des autres).</summary>
    Task<UserCharacter?> GetCharacterAsync(long battleNetId, long characterId, CancellationToken ct);
}

/// <summary>Personnages d'un compte Battle.net, avec le jeton de l'utilisateur (scope wow.profile).</summary>
public interface IAccountCharactersSource
{
    /// <exception cref="UnauthorizedAccessException">Jeton expiré ou révoqué : l'utilisateur doit se reconnecter.</exception>
    Task<IReadOnlyList<UserCharacter>> GetCharactersAsync(string userAccessToken, CancellationToken ct);
}

/// <summary>Tout ce que l'analyse lit d'un personnage : API Blizzard (équipement, talents, fiche) et Raider.io (M+).</summary>
public interface ICharacterDetailsSource
{
    /// <summary>
    /// Les talents sont ceux de <paramref name="tankSpec"/>, même si le personnage joue une autre spécialisation :
    /// Blizzard expose les configurations de toutes les spécialisations. null si le personnage est introuvable.
    /// </summary>
    Task<CharacterDetails?> GetAsync(string region, string realmSlug, string name, TankSpec tankSpec, CancellationToken ct);
}

public sealed record CharacterDetails(
    string Name,
    string Region,
    string RealmSlug,
    int? ActiveSpecId,
    string? ActiveSpecName,
    CharacterLoadout Loadout,
    CharacterSheet? Sheet,
    string? PortraitUrl,
    string? AvatarUrl,
    MythicPlusProfile? MythicPlus);

/// <summary>Fiche de personnage complète (API Blizzard /statistics). Pourcentages tels qu'affichés en jeu.</summary>
public sealed record CharacterSheet(
    double Health,
    double Stamina,
    double Strength,
    double Agility,
    double Armor,
    StatSheet Secondary,
    double VersatilityDamageTaken,
    double Dodge,
    double Parry,
    double Block,
    double Leech,
    double Avoidance,
    double Speed);

public sealed record MythicPlusProfile(
    double Score,
    string? ScoreColor,
    double TankScore,
    string? ProfileUrl,
    int? TankRankWorld,
    int? ClassTankRankWorld,
    IReadOnlyList<MythicPlusRun> BestRuns,
    IReadOnlyList<string> MissingDungeons);

public sealed record MythicPlusRun(string Dungeon, string ShortName, int Level, int Upgrades, double Score, string? Url);
