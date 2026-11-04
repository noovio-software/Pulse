namespace Noovio.Pulse.Application.Meta;

public sealed class MetaOptions
{
    public const string SectionName = "Meta";

    public int TopItemsPerSlot { get; set; } = 5;
    public int TopTrinketsPerOrientation { get; set; } = 5;
    public int TopEmbellishmentCombos { get; set; } = 3;
    public int TopTalentBuilds { get; set; } = 3;

    /// <summary>Bijoux forcés du côté du rôle ou des dégâts, prioritaires sur l'heuristique des mots-clés du rôle.</summary>
    public HashSet<int> RoleTrinketIds { get; set; } = [];
    public HashSet<int> DamageTrinketIds { get; set; } = [];
}
