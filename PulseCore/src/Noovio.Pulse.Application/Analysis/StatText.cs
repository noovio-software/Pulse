using System.Text.RegularExpressions;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Enums;

namespace Noovio.Pulse.Application.Analysis;

/// <summary>
/// Lit les caractéristiques secondaires dans les textes Blizzard des gemmes et enchantements
/// (« +16 à la Hâte et +7 au score de Coup critique », « Enchanted: +147 Haste ») : l'API ne les donne pas autrement.
/// </summary>
public static partial class StatText
{
    /// <summary>Caractéristiques chiffrées du texte, dans l'ordre. Vide si le texte n'en donne pas (effet spécial, endurance…).</summary>
    public static IReadOnlyList<ItemStat> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return Bonus().Matches(Label(text))
            .Select(m => (Value: int.Parse(Spaces().Replace(m.Groups["value"].Value, "")), Stat: ParseStat(m.Groups["name"].Value)))
            .Where(s => s.Stat is not null)
            .Select(s => new ItemStat(s.Stat!.Value, s.Value))
            .ToList();
    }

    /// <summary>Retire le marqueur de rang de qualité de Blizzard (<c>|A:Professions-…|a</c>), illisible hors du jeu.</summary>
    public static string Label(string text) => Markup().Replace(text, "").Trim();

    /// <summary>Texte d'enchantement sans son préfixe (« Enchanté : ») : pour un affichage court.</summary>
    public static string ShortLabel(string text) => Prefix().Replace(Label(text), "");

    /// <summary><paramref name="name"/> : ce qui suit la valeur, jusqu'au bonus suivant (« à la Hâte et », « au score de Coup critique »).</summary>
    private static SecondaryStat? ParseStat(string name) =>
        name.Contains("hâte", StringComparison.OrdinalIgnoreCase) || name.Contains("haste", StringComparison.OrdinalIgnoreCase) ? SecondaryStat.Haste
        : name.Contains("polyvalence", StringComparison.OrdinalIgnoreCase) || name.Contains("versatility", StringComparison.OrdinalIgnoreCase) ? SecondaryStat.Versatility
        : name.Contains("maîtrise", StringComparison.OrdinalIgnoreCase) || name.Contains("mastery", StringComparison.OrdinalIgnoreCase) ? SecondaryStat.Mastery
        : name.Contains("critique", StringComparison.OrdinalIgnoreCase) || name.Contains("crit", StringComparison.OrdinalIgnoreCase) ? SecondaryStat.Crit
        : null;

    // Le séparateur de milliers français est une espace (parfois insécable) : « +1 200 Hâte ».
    [GeneratedRegex(@"\+\s*(?<value>\d[\d\s  ]*)(?<name>[^+]*)")]
    private static partial Regex Bonus();

    [GeneratedRegex(@"[\s  ]")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\|A:.*?\|a")]
    private static partial Regex Markup();

    [GeneratedRegex(@"^(Enchanté|Enchanted)\s*:\s*", RegexOptions.IgnoreCase)]
    private static partial Regex Prefix();
}
