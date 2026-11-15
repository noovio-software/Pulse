using System.Runtime.CompilerServices;
using Noovio.Pulse.Domain.Enums;
using Noovio.Pulse.Domain.Resources;

namespace Noovio.Pulse.Domain.Roles;

/// <summary>Ce que fait le rôle en groupe : décide des rangs Raider.io lus, de la polyvalence affichée et des lignes défensives de la fiche.</summary>
public enum RoleKind
{
    Tank,
    Healer,
    Damage
}

/// <summary>
/// Lignes de la feuille de personnage Blizzard qui font foi : le critique et la hâte n'y sont pas les mêmes selon la façon
/// de combattre. La polyvalence, elle, dépend du rôle (soins pour un soigneur, dégâts sinon).
/// </summary>
public enum StatBasis
{
    /// <summary><c>melee_crit</c>, <c>melee_haste</c>.</summary>
    Melee,

    /// <summary><c>spell_crit</c>, <c>spell_haste</c>.</summary>
    Spell,

    /// <summary><c>ranged_crit</c>, <c>ranged_haste</c> : chasseurs à distance.</summary>
    Ranged
}

/// <summary>
/// Ce qui distingue un rôle pour le moteur : qui lire dans le classement, comment lire sa feuille, ses bijoux et le Journal
/// d'aventure, et comment en parler. Le reste (agrégation, optimisation, roadmap, pages) est commun.
/// </summary>
/// <param name="RaiderIoRole">Valeur de <c>roster[].role</c> chez Raider.io, aussi nom du segment de score et du rang.</param>
/// <param name="StatBasis">Lignes de la feuille lues par défaut ; une spécialisation peut en préciser d'autres (<see cref="SpecInfo.Basis"/>).</param>
/// <param name="JournalHeadings">Mots qui désignent le rôle dans les titres de sections du Journal d'aventure (fr_FR et en_US).</param>
/// <param name="TrinketKeywords">
/// Un bijou dont un effet contient l'un de ces mots sert le rôle. Les descriptions Blizzard ne portent pas de rôle : c'est une
/// heuristique, corrigeable par la configuration (<c>RoleTrinketIds</c>, <c>DamageTrinketIds</c>).
/// </param>
public sealed record PulseRole(
    string RaiderIoRole,
    RoleKind Kind,
    IReadOnlyList<SpecInfo> Specs,
    StatBasis StatBasis,
    IReadOnlyList<string> JournalHeadings,
    IReadOnlyList<string> TrinketKeywords,
    BossHitRule BossHits,
    RoleWording Wording)
{
    public SpecInfo? FromSlug(string? slug) =>
        Specs.FirstOrDefault(s => string.Equals(s.Slug, slug, StringComparison.OrdinalIgnoreCase));

    public bool Includes(int specId) => Specs.Any(s => (int)s.Spec == specId);

    /// <summary>Lignes de la feuille de personnage à lire pour cette spécialisation.</summary>
    public StatBasis BasisOf(WowSpec spec) => Specs.FirstOrDefault(s => s.Spec == spec)?.Basis ?? StatBasis;

    /// <summary>Les spécialisations du rôle pour une classe : aucune, une, deux (prêtre soigneur) ou trois (mage DPS).</summary>
    public IReadOnlyList<SpecInfo> ForClass(int classId) => Specs.Where(s => s.ClassId == classId).ToList();
}

/// <summary>
/// Comment reconnaître, dans une puce du Journal d'aventure, un moment de boss qui concerne le rôle.
/// Une puce est écartée si elle contient un mot de <see cref="Excluded"/> ; sinon elle marque un point par groupe de
/// <see cref="Signals"/> reconnu (au moins un est requis), et deux de plus si elle contient un mot de <see cref="Heavy"/>.
/// </summary>
public sealed record BossHitRule(
    IReadOnlyList<string> Excluded,
    IReadOnlyList<IReadOnlyList<string>> Signals,
    IReadOnlyList<string> Heavy,
    int Threshold)
{
    public int Score(string bullet)
    {
        if (Has(bullet, Excluded))
        {
            return 0;
        }

        var signals = Signals.Count(group => Has(bullet, group));
        return signals == 0 ? 0 : signals + (Has(bullet, Heavy) ? 2 : 0);
    }

    private static bool Has(string text, IReadOnlyList<string> words) => words.Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Le vocabulaire du rôle dans les textes affichés, dans la langue de l'interface : ressources <c>Role_{Key}_{Membre}</c> de
/// <see cref="GameTexts"/>. Chaque langue place ces mots dans ses propres phrases (en français, « ton {Noun} », « bijou {RoleTrinket} »).
/// </summary>
/// <param name="Key">« Tank », « Healer », « Damage » : préfixe des ressources.</param>
public sealed class RoleWording(string Key)
{
    /// <summary>« tank », « soigneur », « DPS » : en français, masculin, après « ton » ou « un ».</summary>
    public string Noun => Text();

    public string NounPlural => Text();

    /// <summary>« tanker », « soigner » : après « peut ».</summary>
    public string Verb => Text();

    /// <summary>Nombre de spécialisations en toutes lettres, dans les titres.</summary>
    public string SpecCount => Text();

    /// <summary>Classes qui ont le rôle, pour le compte sans personnage éligible.</summary>
    public string Classes => Text();

    /// <summary>Ce que le niveau d'objet fait gagner, après « pèse directement sur ».</summary>
    public string ItemLevelImpact => Text();

    /// <summary>Titre de la colonne des bijoux qui servent le rôle.</summary>
    public string RoleTrinkets => Text();

    /// <summary>Qualificatif d'un bijou du rôle : « défensif », « de soin », « de dégâts ».</summary>
    public string RoleTrinket => Text();

    /// <summary>Titre de la colonne des autres bijoux.</summary>
    public string DamageTrinkets => Text();

    /// <summary>Qualificatif d'un autre bijou : « offensif », « de dégâts », « de caractéristiques ».</summary>
    public string DamageTrinket => Text();

    /// <summary>Le top ne joue aucun bijou du rôle.</summary>
    public string NoRoleTrinket => Text();

    /// <summary>Quand activer un bijou du rôle ; {0} : sa recharge.</summary>
    public string RoleTrinketAdvice => Text();

    /// <summary>Quand activer un autre bijou ; {0} : sa recharge.</summary>
    public string DamageTrinketAdvice => Text();

    /// <summary>Titre de la liste des moments de boss.</summary>
    public string HitsTitle => Text();

    /// <summary>Note de cette liste, quand le joueur a un bijou du rôle à activer.</summary>
    public string HitsWithTrinket => Text();

    /// <summary>Note de cette liste, sinon.</summary>
    public string HitsWithoutTrinket => Text();

    /// <summary>Page d'erreur, pour la touche d'humour.</summary>
    public string ServerError => Text();

    /// <summary>Le nom de ressource de chaque membre, pour vérifier qu'aucune langue n'en oublie.</summary>
    public IEnumerable<string> ResourceNames =>
        typeof(RoleWording).GetProperties()
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => ResourceName(p.Name));

    private string ResourceName(string member) => $"Role_{Key}_{member}";

    private string Text([CallerMemberName] string member = "") =>
        GameTexts.ResourceManager.GetString(ResourceName(member), GameTexts.Culture) ?? ResourceName(member);
}

/// <summary>Une application Pulse : un nom, et le rôle dont elle suit la méta. Le nom donne les clés de configuration, de cache et de cookie.</summary>
/// <param name="Name">« FrontPulse » : section de configuration et chaîne de connexion.</param>
/// <param name="Sigil">Tracé SVG de l'emblème (viewBox 32×32), traversé par le pouls commun à toutes les applications.</param>
public sealed record PulseApp(string Name, PulseRole Role, string Sigil)
{
    /// <summary>« frontpulse » : préfixe de cache, nom de cookie, unités systemd.</summary>
    public string Key => Name.ToLowerInvariant();

    public RoleWording Words => Role.Wording;

    /// <summary>Routes de donjons Keystone.guru : synchronisation dans le worker, pages et liens sur le site.</summary>
    public bool HasDungeonRoutes { get; init; }
}
