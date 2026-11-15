namespace Noovio.Pulse.Application.Import;

/// <summary>
/// Personnage exporté par l'addon NoovioPulse (format 1), tel que lu en jeu : équipement, talents,
/// caractéristiques. Les noms de champs sont ceux du JSON (snake_case). Tous les textes sont dans la langue du client.
/// </summary>
public sealed record CharacterImport(
    int V,
    string? Kind,
    string? Addon,
    string? Game,
    string? Locale,
    long? ExportedAt,
    ImportedIdentity? Character,
    IReadOnlyList<ImportedGear>? Gear,
    ImportedTalents? Talents,
    ImportedStats? Stats,
    ImportedSheet? Sheet,
    ImportedBags? Bags = null,
    ImportedVault? Vault = null)
{
    public DateTimeOffset? ExportedAtUtc => ExportedAt is > 0 ? DateTimeOffset.FromUnixTimeSeconds(ExportedAt.Value) : null;
}

/// <summary><see cref="Region"/> : us, eu, kr, tw, cn. <see cref="Spec"/> : identifiant Blizzard de la spécialisation active.</summary>
public sealed record ImportedIdentity(
    string? Name,
    string? Realm,
    string? RealmNormalized,
    string? Region,
    int Class,
    string? Race,
    string? Faction,
    int Level,
    int Spec,
    string? SpecName,
    double Ilvl);

/// <summary><see cref="Slot"/> : type d'emplacement de l'API Blizzard (HEAD, FINGER_1…).</summary>
public sealed record ImportedGear(
    string? Slot,
    int Id,
    string? Name,
    int Ilvl,
    IReadOnlyList<int>? Bonus,
    int? EnchantId,
    string? Enchant,
    IReadOnlyList<ImportedSocket>? Gems,
    IReadOnlyDictionary<string, double>? Stats,
    IReadOnlyList<ImportedEffect>? Effects,
    bool Embellished,
    int? SetId = null,
    bool Usable = true,
    ImportedUpgrade? Upgrade = null);

/// <summary>
/// Piste d'amélioration (« Héros 2/6 ») : <see cref="MaxIlvl"/> quand le jeu l'a donné, sinon le site l'estime
/// à partir des autres pièces de la même piste.
/// </summary>
public sealed record ImportedUpgrade(string? Track, int Rank, int MaxRank, int? MaxIlvl);

/// <summary>Contenu des sacs : équipement mettable (mêmes champs que l'équipement porté) et consommables regroupés par objet.</summary>
public sealed record ImportedBags(IReadOnlyList<ImportedGear>? Gear, IReadOnlyList<ImportedConsumable>? Consumables);

/// <summary>
/// <see cref="Kind"/> : potion, elixir, flask, food, enhancement (huiles, pierres) ou other (runes…) ; <see cref="Quality"/> : rang
/// d'artisanat ; <see cref="Current"/> : de l'extension en cours ; <see cref="Use"/> : effet « Utiliser : » dans la langue du client.
/// </summary>
public sealed record ImportedConsumable(int Id, string? Name, string? Kind, int Count, int? Quality, int? MinLevel, bool? Current, string? Use);

/// <summary>Grande Chambre forte : <see cref="HasRewards"/> quand un coffre attend d'être ouvert (objets dans les activités).</summary>
public sealed record ImportedVault(bool HasRewards, bool CanClaim, IReadOnlyList<ImportedVaultActivity>? Activities);

/// <summary>
/// Une case : <see cref="Type"/> 1 donjons, 3 raid, 6 monde ; <see cref="ExampleIlvl"/> : niveau d'objet promis ;
/// <see cref="Items"/> : objets proposés quand le coffre est prêt.
/// </summary>
public sealed record ImportedVaultActivity(int Type, int Index, int Threshold, int Progress, int Level, int? ExampleIlvl, IReadOnlyList<ImportedGear>? Items);

/// <summary>Une châsse : <see cref="Id"/> null quand elle est vide ; <see cref="Text"/> : bonus affiché (« +70 Hâte »).</summary>
public sealed record ImportedSocket(int? Id, string? Name, string? Text, string? Type);

/// <summary><see cref="Trigger"/> : use ou equip ; <see cref="Text"/> : description sans le préfixe « Utiliser : ».</summary>
public sealed record ImportedEffect(string? Trigger, int Spell, string? Name, string? Text);

/// <summary>
/// <see cref="Nodes"/> : un tableau par nœud choisi, [nœud, entrée, sort, rang, offert] ; « offert » vaut 1 pour un nœud
/// accordé d'office (aucun point dépensé).
/// </summary>
public sealed record ImportedTalents(string? Code, string? Hero, int? HeroId, IReadOnlyList<IReadOnlyList<long>>? Nodes);

/// <summary>Indices de la feuille et pourcentages par façon de combattre : le site garde ceux de la spécialisation.</summary>
public sealed record ImportedStats(ImportedLines? Haste, ImportedLines? Crit, ImportedMastery? Mastery, ImportedVersatility? Versatility);

public sealed record ImportedLines(double Rating, double Melee, double Ranged, double Spell);

public sealed record ImportedMastery(double Rating, double Percent);

public sealed record ImportedVersatility(double Rating, double Done, double Taken);

public sealed record ImportedSheet(
    double Health,
    double Strength,
    double Agility,
    double Stamina,
    double Intellect,
    double Mana,
    double Armor,
    double Dodge,
    double Parry,
    double Block,
    double Leech,
    double Avoidance,
    double Speed);

/// <summary>Résultat de la lecture d'une chaîne : l'import, ou un message d'erreur à afficher.</summary>
public sealed record CharacterImportResult(CharacterImport? Import, string? Error)
{
    public bool Succeeded => Import is not null;
}
