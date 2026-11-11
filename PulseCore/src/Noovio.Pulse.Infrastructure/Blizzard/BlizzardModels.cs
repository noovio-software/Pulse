namespace Noovio.Pulse.Infrastructure.Blizzard;

public sealed class BzToken
{
    public string AccessToken { get; set; } = "";
    public int ExpiresIn { get; set; }
}

public sealed class BzRef
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public sealed class BzTyped
{
    public string? Type { get; set; }
    public string? Name { get; set; }
}

public sealed class BzEquipment
{
    public List<BzEquippedItem> EquippedItems { get; set; } = [];
}

public sealed class BzEquippedItem
{
    public BzTyped Slot { get; set; } = new();
    public BzRef Item { get; set; } = new();
    public string Name { get; set; } = "";
    public BzValue? Level { get; set; }
    public List<int> BonusList { get; set; } = [];
    public List<BzEnchantment> Enchantments { get; set; } = [];
    public List<BzItemSpell> Spells { get; set; } = [];
    public List<BzSocket> Sockets { get; set; } = [];
    public List<BzItemStat> Stats { get; set; } = [];

    /// <summary>Ex. « Unique-Equipped: Embellished (2) » sur les pièces embellies.</summary>
    public string? LimitCategory { get; set; }
}

public sealed class BzValue
{
    public int Value { get; set; }
}

public sealed class BzEnchantment
{
    public string? DisplayString { get; set; }
}

public sealed class BzItemSpell
{
    public BzRef Spell { get; set; } = new();
    public string? Description { get; set; }
}

public sealed class BzSocket
{
    public BzTyped? SocketType { get; set; }

    /// <summary>Absent quand la châsse est vide.</summary>
    public BzRef? Item { get; set; }
    public string? DisplayString { get; set; }
}

public sealed class BzItemStat
{
    public BzTyped? Type { get; set; }
    public int Value { get; set; }
    public bool IsNegated { get; set; }
}

public sealed class BzSpecializations
{
    public List<BzSpecializationEntry> Specializations { get; set; } = [];
    public BzRef? ActiveSpecialization { get; set; }
    public BzRef? ActiveHeroTalentTree { get; set; }
}

public sealed class BzSpecializationEntry
{
    public BzRef Specialization { get; set; } = new();
    public List<BzLoadout> Loadouts { get; set; } = [];
}

public sealed class BzLoadout
{
    public bool IsActive { get; set; }
    public string? TalentLoadoutCode { get; set; }
    public BzRef? SelectedHeroTalentTree { get; set; }
    public List<BzSelectedTalent> SelectedClassTalents { get; set; } = [];
    public List<BzSelectedTalent> SelectedSpecTalents { get; set; } = [];
    public List<BzSelectedTalent> SelectedHeroTalents { get; set; } = [];
}

public sealed class BzSelectedTalent
{
    public int Id { get; set; }
    public int Rank { get; set; }
    public BzTalentTooltip? Tooltip { get; set; }
}

public sealed class BzTalentTooltip
{
    public BzRef? Talent { get; set; }
    public BzSpellTooltip? SpellTooltip { get; set; }
}

public sealed class BzSpellTooltip
{
    public BzRef? Spell { get; set; }
}

public sealed class BzStatistics
{
    public double Health { get; set; }

    /// <summary>Réserve de la ressource principale : le mana, pour un soigneur.</summary>
    public double Power { get; set; }

    public BzEffectiveStat? Strength { get; set; }
    public BzEffectiveStat? Agility { get; set; }
    public BzEffectiveStat? Intellect { get; set; }
    public BzEffectiveStat? Stamina { get; set; }
    public BzEffectiveStat? Armor { get; set; }

    public BzRatingStat? MeleeCrit { get; set; }
    public BzRatingStat? MeleeHaste { get; set; }
    public BzRatingStat? SpellCrit { get; set; }
    public BzRatingStat? SpellHaste { get; set; }
    public BzRatingStat? RangedCrit { get; set; }
    public BzRatingStat? RangedHaste { get; set; }
    public BzRatingStat? Mastery { get; set; }

    /// <summary>Indice de polyvalence (le seul secondaire exposé en nombre simple).</summary>
    public double Versatility { get; set; }
    public double VersatilityDamageDoneBonus { get; set; }
    public double VersatilityHealingDoneBonus { get; set; }
    public double VersatilityDamageTakenBonus { get; set; }

    public BzRatingStat? Dodge { get; set; }
    public BzRatingStat? Parry { get; set; }
    public BzRatingStat? Block { get; set; }
    public BzRatingStat? Lifesteal { get; set; }
    public BzRatingStat? Avoidance { get; set; }
    public BzRatingStat? Speed { get; set; }
}

/// <summary>
/// <see cref="Value"/> : pourcentage de la feuille de personnage (base comprise) ;
/// l'indice investi est dans <c>rating_normalized</c> (il n'y a pas de champ <c>rating</c>).
/// </summary>
public sealed class BzRatingStat
{
    [System.Text.Json.Serialization.JsonPropertyName("rating_normalized")]
    public double Rating { get; set; }

    public double RatingBonus { get; set; }
    public double Value { get; set; }
}

public sealed class BzEffectiveStat
{
    public double Base { get; set; }
    public double Effective { get; set; }
}

public sealed class BzCharacterMedia
{
    /// <summary>Clés : avatar, inset, main-raw.</summary>
    public List<BzAsset> Assets { get; set; } = [];
}

public sealed class BzAccountProfile
{
    public List<BzWowAccount> WowAccounts { get; set; } = [];
}

public sealed class BzWowAccount
{
    public List<BzAccountCharacter> Characters { get; set; } = [];
}

public sealed class BzAccountCharacter
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public BzRealm Realm { get; set; } = new();
    public BzRef? PlayableClass { get; set; }
    public BzRef? PlayableRace { get; set; }
    public BzTyped? Faction { get; set; }
    public int Level { get; set; }
}

public sealed class BzRealm
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string Slug { get; set; } = "";
}

public sealed class BzKey
{
    public string Href { get; set; } = "";
}

public sealed class BzTalentTreeIndex
{
    public List<BzTalentTreeIndexEntry> SpecTalentTrees { get; set; } = [];
}

/// <summary>Lien vers <c>/data/wow/talent-tree/{treeId}/playable-specialization/{specId}</c>.</summary>
public sealed class BzTalentTreeIndexEntry
{
    public BzKey Key { get; set; } = new();
    public string? Name { get; set; }
}

public sealed class BzTalentTree
{
    public string? Name { get; set; }
    public BzRef? PlayableClass { get; set; }
    public List<BzTalentNode> ClassTalentNodes { get; set; } = [];
    public List<BzTalentNode> SpecTalentNodes { get; set; } = [];
    public List<BzHeroTalentTree> HeroTalentTrees { get; set; } = [];
}

public sealed class BzHeroTalentTree
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<BzTalentNode> HeroTalentNodes { get; set; } = [];
}

public sealed class BzTalentNode
{
    public int Id { get; set; }
    public List<int> Unlocks { get; set; } = [];

    /// <summary>ACTIVE, PASSIVE, CHOICE.</summary>
    public BzTyped? NodeType { get; set; }

    public List<BzNodeRank> Ranks { get; set; } = [];

    /// <summary>Multiples de 300 ; une colonne ou une rangée = 600.</summary>
    public double RawPositionX { get; set; }
    public double RawPositionY { get; set; }
}

public sealed class BzNodeRank
{
    public int Rank { get; set; }
    public BzTalentTooltip? Tooltip { get; set; }
    public List<BzTalentTooltip> ChoiceOfTooltips { get; set; } = [];
}

public sealed class BzMedia
{
    public List<BzAsset> Assets { get; set; } = [];
}

public sealed class BzAsset
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class BzJournalInstanceIndex
{
    public List<BzRef> Instances { get; set; } = [];
}

public sealed class BzJournalInstance
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<BzRef> Encounters { get; set; } = [];
}

public sealed class BzJournalEncounter
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<BzJournalItem> Items { get; set; } = [];
    public List<BzJournalSection> Sections { get; set; } = [];
}

public sealed class BzJournalItem
{
    public BzRef Item { get; set; } = new();
}

/// <summary>Arbre des techniques d'une rencontre. Aucun champ de rôle ni de difficulté : seuls le titre et le texte les évoquent.</summary>
public sealed class BzJournalSection
{
    public string? Title { get; set; }
    public string? BodyText { get; set; }
    public List<BzJournalSection> Sections { get; set; } = [];
}
