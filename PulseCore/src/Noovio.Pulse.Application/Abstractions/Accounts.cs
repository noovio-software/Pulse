using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Enums;

namespace Noovio.Pulse.Application.Abstractions;

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
    /// Les talents sont ceux de <paramref name="spec"/>, même si le personnage joue une autre spécialisation :
    /// Blizzard expose les configurations de toutes les spécialisations. null si le personnage est introuvable.
    /// </summary>
    Task<CharacterDetails?> GetAsync(string region, string realmSlug, string name, WowSpec spec, CancellationToken ct);
}

/// <summary>
/// Ce que l'addon ne peut pas lire en jeu, retrouvé par nom, royaume et région : portrait (Blizzard) et Mythique+ (Raider.io).
/// Sert à compléter un import ; chaque partie peut manquer (personnage introuvable, service indisponible).
/// </summary>
public interface ICharacterExtrasSource
{
    Task<CharacterExtras> GetExtrasAsync(string region, string realmSlug, string name, CancellationToken ct);
}

public sealed record CharacterExtras(string? PortraitUrl, string? AvatarUrl, MythicPlusProfile? MythicPlus)
{
    public static readonly CharacterExtras None = new(null, null, null);
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
    double Intellect,
    double Mana,
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
    double RoleScore,
    string? ProfileUrl,
    int? RoleRankWorld,
    int? ClassRoleRankWorld,
    IReadOnlyList<MythicPlusRun> BestRuns,
    IReadOnlyList<string> MissingDungeons);

public sealed record MythicPlusRun(string Dungeon, string ShortName, int Level, int Upgrades, double Score, string? Url);
