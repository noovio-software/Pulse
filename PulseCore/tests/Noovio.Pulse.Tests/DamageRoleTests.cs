using System.Text.Json;
using Microsoft.Extensions.Options;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Analysis;
using Noovio.Pulse.Application.Meta;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Enums;
using Noovio.Pulse.Domain.Roles;
using Noovio.Pulse.Infrastructure.Blizzard;
using Noovio.Pulse.Infrastructure.RaiderIo;

namespace Noovio.Pulse.Tests;

/// <summary>Ce que le rôle DPS change au moteur : vingt-sept spécialisations, trois façons de lire la feuille, des bijoux de dégâts.</summary>
public sealed class DamageRoleTests
{
    private static readonly PulseRole Damage = PulseRoles.Damage;

    [Fact]
    public void Every_damage_spec_is_tracked_once() =>
        Assert.Equal(27, Damage.Specs.DistinctBy(s => s.Spec).Count());

    [Theory]
    [InlineData(8, new[] { WowSpec.ArcaneMage, WowSpec.FireMage, WowSpec.FrostMage })]
    [InlineData(12, new[] { WowSpec.HavocDemonHunter, WowSpec.DevourerDemonHunter })]
    [InlineData(5, new[] { WowSpec.ShadowPriest })]
    [InlineData(1, new[] { WowSpec.ArmsWarrior, WowSpec.FuryWarrior })]
    public void Every_class_has_its_damage_specs(int classId, WowSpec[] expected) =>
        Assert.Equal(expected, Damage.ForClass(classId).Select(s => s.Spec));

    [Fact]
    public void Every_spec_is_listed_once_across_roles()
    {
        var specs = WowSpecs.Tanks.Concat(WowSpecs.Healers).Concat(WowSpecs.Damage).Select(s => s.Spec).ToList();
        Assert.Equal(Enum.GetValues<WowSpec>().Order(), specs.Order());
    }

    [Theory]
    [InlineData(WowSpec.FuryWarrior, StatBasis.Melee)]
    [InlineData(WowSpec.SurvivalHunter, StatBasis.Melee)]
    [InlineData(WowSpec.MarksmanshipHunter, StatBasis.Ranged)]
    [InlineData(WowSpec.BeastMasteryHunter, StatBasis.Ranged)]
    [InlineData(WowSpec.FireMage, StatBasis.Spell)]
    [InlineData(WowSpec.DevourerDemonHunter, StatBasis.Spell)]
    [InlineData(WowSpec.HavocDemonHunter, StatBasis.Melee)]
    public void Each_spec_reads_the_lines_of_how_it_fights(WowSpec spec, StatBasis expected) =>
        Assert.Equal(expected, Damage.BasisOf(spec));

    [Fact]
    public void The_other_roles_keep_their_own_lines()
    {
        Assert.Equal(StatBasis.Spell, PulseRoles.Healer.BasisOf(WowSpec.HolyPriest));
        Assert.Equal(StatBasis.Melee, PulseRoles.Tank.BasisOf(WowSpec.BloodDeathKnight));
    }

    /// <summary>Un lanceur de sorts lit ses lignes de sort, mais la polyvalence de ses dégâts ; un chasseur, ses lignes à distance.</summary>
    [Fact]
    public void A_damage_dealer_reads_the_lines_of_its_spec_and_damage_versatility()
    {
        const string json = """
            {
              "melee_crit": { "rating_normalized": 800, "value": 10 },
              "melee_haste": { "rating_normalized": 900, "value": 11 },
              "spell_crit": { "rating_normalized": 800, "value": 20 },
              "spell_haste": { "value": 21 },
              "ranged_crit": { "rating_normalized": 800, "value": 30 },
              "ranged_haste": { "rating_normalized": 900, "value": 31 },
              "mastery": { "rating_normalized": 700, "value": 40 },
              "versatility": 300,
              "versatility_damage_done_bonus": 6,
              "versatility_healing_done_bonus": 7
            }
            """;
        var stats = JsonSerializer.Deserialize<BzStatistics>(json, RaiderIoClient.JsonOptions)!;

        var caster = BlizzardCharacterSource.ToStats(stats, StatBasis.Spell, healing: false);
        var hunter = BlizzardCharacterSource.ToStats(stats, StatBasis.Ranged, healing: false);

        Assert.Equal((800d, 20d), (caster.Crit.Rating, caster.Crit.Percent));
        Assert.Equal((900d, 21d), (caster.Haste.Rating, caster.Haste.Percent));
        Assert.Equal(6, caster.Versatility.Percent);
        Assert.Equal((30d, 31d), (hunter.Crit.Percent, hunter.Haste.Percent));
    }

    [Theory]
    [InlineData("Utiliser : Vous lancez une lance qui inflige 120000 points de dégâts de Feu à votre cible.", TrinketOrientation.Role)]
    [InlineData("Équipé : Vos attaques ont une chance d'infliger 30000 points de dégâts d'Ombre.", TrinketOrientation.Role)]
    [InlineData("Équipé : vos sorts infligent des dégâts d'Ombre, selon vos besoins.", TrinketOrientation.Role)]
    [InlineData("Équipé : Vos dégâts ont une très forte chance d'entrer en résonance avec le tentacule et de conférer Puissance dévastatrice.", TrinketOrientation.Damage)]
    [InlineData("Utiliser : Vous buvez une gorgée, ce qui augmente votre score de Coup critique de 653 pendant 15 s.", TrinketOrientation.Damage)]
    [InlineData("Équipé : Vos attaques ont une chance d'augmenter votre Agilité de 900 pendant 15 s.", TrinketOrientation.Damage)]
    public void Damage_trinkets_are_split_between_damage_and_stats(string effect, TrinketOrientation expected)
    {
        var aggregator = new MetaAggregatorService(Options.Create(new MetaOptions()), Damage);
        Assert.Equal(expected, aggregator.Orientation(new GearPiece(GearSlot.Trinket, 42, "Bijou", 700, [], [], [new ItemEffect(1, "Effet", effect)], false)));
    }

    [Theory]
    // Textes réels du journal (saison 2 de Midnight), section « DPS ».
    [InlineData("$bullet;Les Aqir qui n’ont pas été tués avant que Kith’ix ne lance [Ténèbres suffocantes] se renforcent grandement.", "Ténèbres suffocantes")]
    [InlineData("$bullet;Rav’i arrête sa [Vorasssité charognarde] ou sa [Frénésie dévorante] lorsque son bouclier absorbant les dégâts est épuisé.", "Vorasssité charognarde")]
    [InlineData("$bullet;[Éclosion] libère des serpents qui se joignent au combat.", "Éclosion")]
    [InlineData("$bullet;[Appel des éléments] invoque trois totems élémentaires aux effets puissants.", "Appel des éléments")]
    [InlineData("$bullet;[Racines épine-sanglante] immobilise les personnages-joueurs jusqu’à la destruction des racines.", "Racines épine-sanglante")]
    public void Adds_targets_and_shields_are_burst_moments(string text, string ability)
    {
        var boss = new JournalBoss(1, "Boss", [], [new JournalAbility("DPS", text, true)]);
        Assert.Equal(ability, TrinketPlanner.Hit(boss, Damage.BossHits)?.Ability);
    }

    [Theory]
    [InlineData("$bullet;[Tempête gelée] inflige de lourds dégâts et repousse les personnages-joueurs.")]
    [InlineData("$bullet;Interrompez les [Images miroirs] de Kystia.")]
    // « situées » contient « tué » : seul le mot isolé compte.
    [InlineData("$bullet;Des ombres lancent [Célérité du Néant], infligeant des dégâts aux cibles situées en ligne droite devant elles.")]
    public void Damage_to_dodge_or_interrupts_are_not_burst_moments(string text)
    {
        var boss = new JournalBoss(1, "Boss", [], [new JournalAbility("DPS", text, true)]);
        Assert.Null(TrinketPlanner.Hit(boss, Damage.BossHits));
    }

    [Theory]
    [InlineData("DPS")]
    [InlineData("Damage Dealers")]
    public void Every_real_journal_heading_of_the_role_is_recognised(string heading) =>
        Assert.Contains(Damage.JournalHeadings, h => heading.Contains(h, StringComparison.OrdinalIgnoreCase));
}
