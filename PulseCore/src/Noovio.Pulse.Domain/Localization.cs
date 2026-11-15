using System.Globalization;
using Noovio.Pulse.Domain.Enums;
using Noovio.Pulse.Domain.Resources;

namespace Noovio.Pulse.Domain;

/// <summary>Libellés du jeu dans la langue de l'interface (<see cref="CultureInfo.CurrentUICulture"/>).</summary>
public static class GameLabels
{
    /// <summary>Un emplacement : « Anneau », « Ring ».</summary>
    public static string Slot(GearSlot slot) => Get($"Slot_{slot}");

    /// <summary>L'en-tête d'un emplacement, au pluriel quand il compte deux pièces : « Anneaux », « Rings ».</summary>
    public static string SlotGroup(GearSlot slot) => slot is GearSlot.Finger or GearSlot.Trinket ? Get($"SlotGroup_{slot}") : Slot(slot);

    /// <summary>« Hâte », « Haste ».</summary>
    public static string Stat(SecondaryStat stat) => Get($"Stat_{stat}");

    /// <summary>« et » / « and », pour joindre deux ou trois éléments.</summary>
    public static string And => Get("List_And");

    /// <summary>« ou » / « or ».</summary>
    public static string Or => Get("List_Or");

    private static string Get(string name) => GameTexts.ResourceManager.GetString(name, GameTexts.Culture) ?? name;
}

/// <summary>
/// Choix du singulier ou du pluriel selon la langue : en français, 0 et 1 sont au singulier ; en anglais, seul 1 l'est.
/// Les ressources vont par paires : <c>…_One</c> et <c>…_Other</c>.
/// </summary>
public static class Plural
{
    public static bool IsOne(double count, CultureInfo? culture = null) =>
        (culture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName == "fr" ? Math.Abs(count) < 2 : count == 1;

    /// <summary>Formate <paramref name="count"/> (argument {0}) avec la forme qui lui convient.</summary>
    public static string Format(double count, string one, string other, params object?[] more) =>
        string.Format(CultureInfo.CurrentCulture, IsOne(count) ? one : other, [count, .. more]);
}
