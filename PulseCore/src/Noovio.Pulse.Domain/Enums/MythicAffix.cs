namespace Noovio.Pulse.Domain.Enums;

/// <summary>
/// Affixes M+ (identifiants Blizzard, repris par Raider.io dans <c>weekly_modifiers[].id</c>).
/// Seuls les affixes stables d'une saison à l'autre sont nommés ; un identifiant inconnu
/// reste représentable par simple conversion depuis l'entier.
/// </summary>
public enum MythicAffix
{
    Tyrannical = 9,
    Fortified = 10,
    XalatathsGuile = 147
}
