namespace Noovio.Pulse.Domain.Entities;

/// <summary>Compte d'une application Pulse : créé à la première connexion Battle.net, aucun mot de passe stocké.</summary>
public sealed class UserAccount
{
    public long Id { get; set; }

    /// <summary>Identifiant Battle.net (<c>id</c> de /oauth/userinfo), stable même si le BattleTag change.</summary>
    public long BattleNetId { get; set; }

    public string BattleTag { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset LastLoginAtUtc { get; set; }
    public DateTimeOffset? LastSyncAtUtc { get; set; }

    public List<UserCharacter> Characters { get; set; } = [];
}

/// <summary>Personnage d'un compte, tel que listé par /profile/user/wow lors de la dernière synchronisation.</summary>
public sealed class UserCharacter
{
    public long Id { get; set; }
    public long AccountId { get; set; }

    public long BlizzardCharacterId { get; set; }
    public string Region { get; set; } = "";
    public string RealmSlug { get; set; } = "";
    public string RealmName { get; set; } = "";
    public string Name { get; set; } = "";
    public int ClassId { get; set; }
    public string ClassName { get; set; } = "";
    public string? RaceName { get; set; }
    public string? Faction { get; set; }
    public int Level { get; set; }
    public DateTimeOffset SyncedAtUtc { get; set; }
}
