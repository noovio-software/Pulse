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

/// <summary>Ce que le rôle soigneur change au moteur partagé avec les tanks.</summary>
public sealed class HealerRoleTests
{
    private static readonly PulseRole Healer = PulseRoles.Healer;

    [Theory]
    [InlineData(5, new[] { WowSpec.DisciplinePriest, WowSpec.HolyPriest })]
    [InlineData(2, new[] { WowSpec.HolyPaladin })]
    [InlineData(7, new[] { WowSpec.RestorationShaman })]
    [InlineData(11, new[] { WowSpec.RestorationDruid })]
    [InlineData(10, new[] { WowSpec.MistweaverMonk })]
    [InlineData(13, new[] { WowSpec.PreservationEvoker })]
    [InlineData(1, new WowSpec[0])]
    public void Every_healing_class_has_its_specs(int classId, WowSpec[] expected) =>
        Assert.Equal(expected, Healer.ForClass(classId).Select(s => s.Spec));

    [Fact]
    public void Slugs_are_unique_across_roles()
    {
        var slugs = WowSpecs.Tanks.Concat(WowSpecs.Healers).Concat(WowSpecs.Damage).Select(s => s.Slug).ToList();
        Assert.Equal(slugs.Count, slugs.Distinct().Count());
    }

    /// <summary>Extrait fidèle de /statistics : les lignes de mêlée d'un soigneur ne sont pas celles de sa feuille en jeu.</summary>
    [Fact]
    public void A_healer_reads_spell_lines_of_the_character_sheet()
    {
        const string json = """
            {
              "health": 500000, "power": 250000,
              "intellect": { "base": 100, "effective": 9000 },
              "melee_crit": { "rating_normalized": 800, "rating_bonus": 5, "value": 10 },
              "melee_haste": { "rating_normalized": 900, "rating_bonus": 5, "value": 11 },
              "spell_crit": { "rating_normalized": 800, "rating_bonus": 5, "value": 20 },
              "spell_haste": { "rating_bonus": 5, "value": 21 },
              "mastery": { "rating_normalized": 700, "value": 40 },
              "versatility": 300,
              "versatility_damage_done_bonus": 6,
              "versatility_healing_done_bonus": 7,
              "versatility_damage_taken_bonus": 3
            }
            """;
        var stats = JsonSerializer.Deserialize<BzStatistics>(json, RaiderIoClient.JsonOptions)!;

        var healer = BlizzardCharacterSource.ToStats(stats, StatBasis.Spell, healing: true);
        var tank = BlizzardCharacterSource.ToStats(stats, StatBasis.Melee, healing: false);

        Assert.Equal((800d, 20d), (healer.Crit.Rating, healer.Crit.Percent));
        Assert.Equal((900d, 21d), (healer.Haste.Rating, healer.Haste.Percent));
        Assert.Equal(7, healer.Versatility.Percent);
        Assert.Equal(40, healer.Mastery.Percent);

        Assert.Equal(10, tank.Crit.Percent);
        Assert.Equal(11, tank.Haste.Percent);
        Assert.Equal(6, tank.Versatility.Percent);

        Assert.Equal(9000, stats.Intellect!.Effective);
        Assert.Equal(250000, stats.Power);
    }

    [Theory]
    [InlineData("Équipé : vos sorts ont une chance de rendre du mana.", TrinketOrientation.Role)]
    [InlineData("Utiliser : augmente les soins prodigués de 10 %.", TrinketOrientation.Role)]
    [InlineData("Équipé : vos soins critiques soignent un allié proche.", TrinketOrientation.Role)]
    [InlineData("Utiliser : place un bouclier sur l'allié ciblé.", TrinketOrientation.Role)]
    // Textes réels du top : une barrière ou une absorption sur un allié sert le soin, sans contenir le mot.
    [InlineData("Utiliser : Vous invoquez 5 esprits d’adeptes entortillâme en 2 s et sacrifiez leur essence, ce qui accorde une barrière réduisant les dégâts subis de 50 % à un personnage allié pendant 20 s.", TrinketOrientation.Role)]
    [InlineData("Utiliser : Vous déchaînez l’énergie gangrenée stockée dans le cristal pour imprégner une cible alliée pendant 10 s, absorbant 847499 points de dégâts.", TrinketOrientation.Role)]
    [InlineData("Équipé : Vos soins ont une chance élevée d’imprégner la cible d’un antivenin surnaturel pendant 30 s.", TrinketOrientation.Role)]
    [InlineData("Équipé : Vos dégâts ont une très forte chance d’entrer en résonance avec le tentacule et de conférer Puissance dévastatrice.", TrinketOrientation.Damage)]
    [InlineData("Équipé : Vos sorts ont une chance élevée d’invoquer le pouvoir de l’océan et augmente votre Intelligence de 69 jusqu’à 6 fois.", TrinketOrientation.Damage)]
    [InlineData("Utiliser : Vous buvez une gorgée, ce qui augmente votre score de Coup critique de 653 pendant 15 s.", TrinketOrientation.Damage)]
    [InlineData("Utiliser : augmente votre Intelligence de 500 pendant 20 s.", TrinketOrientation.Damage)]
    [InlineData("Équipé : vos sorts infligent des dégâts d'Ombre, selon vos besoins.", TrinketOrientation.Damage)]
    public void Healer_trinkets_are_sorted_by_their_effect(string effect, TrinketOrientation expected)
    {
        var aggregator = new MetaAggregatorService(Options.Create(new MetaOptions()), Healer);
        Assert.Equal(expected, aggregator.Orientation(Trinket(effect)));
    }

    [Fact]
    public void Configured_ids_win_over_the_keywords()
    {
        var options = new MetaOptions { DamageTrinketIds = [42] };
        var aggregator = new MetaAggregatorService(Options.Create(options), Healer);
        Assert.Equal(TrinketOrientation.Damage, aggregator.Orientation(Trinket("Équipé : rend du mana.")));
    }

    [Fact]
    public void Group_damage_is_a_moment_for_a_healer_but_not_for_a_tank()
    {
        const string text = "$bullet;[Onde] inflige des dégâts importants à tous les joueurs.";
        var boss = new JournalBoss(1, "Boss", [], [new JournalAbility("Soigneurs", text, true)]);

        var hit = TrinketPlanner.Hit(boss, Healer.BossHits);

        Assert.Equal("Onde", hit?.Ability);
        Assert.Null(TrinketPlanner.Hit(boss, PulseRoles.Tank.BossHits));
    }

    [Theory]
    // Textes réels du journal : le groupe n'y est pas toujours « tous les joueurs ».
    [InlineData("$bullet;[Alerte stridente] et [Assaut diaphane] infligent des dégâts importants à l’ensemble du groupe.")]
    [InlineData("$bullet;[Battements d’aile] inflige des dégâts de nature modérés à tous les personnages-joueurs.")]
    [InlineData("$bullet;[Morsure vorace] inflige des dégâts importants à sa cible et augmente les dégâts subis.")]
    [InlineData("$bullet;Dissipez [Maléfice] sans attendre.")]
    [InlineData("$bullet;[Entaille] absorbe les soins reçus par sa cible.")]
    public void Dispels_and_healing_absorbs_are_moments_for_a_healer(string text)
    {
        var boss = new JournalBoss(1, "Boss", [], [new JournalAbility("Soigneurs", text, true)]);
        Assert.NotNull(TrinketPlanner.Hit(boss, Healer.BossHits));
    }

    [Fact]
    public void A_tip_without_any_signal_is_not_a_moment()
    {
        var boss = new JournalBoss(1, "Boss", [], [new JournalAbility("Soigneurs", "$bullet;Restez groupés derrière le boss.", true)]);
        Assert.Null(TrinketPlanner.Hit(boss, Healer.BossHits));
    }

    [Theory]
    [InlineData("Soigneurs")]
    [InlineData("Soigneur")]
    [InlineData("Soigneurs/soigneuses")]
    public void Every_real_journal_heading_of_the_role_is_recognised(string heading) =>
        Assert.Contains(Healer.JournalHeadings, h => heading.Contains(h, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void Only_sections_of_the_role_count()
    {
        var boss = new JournalBoss(1, "Boss", [], [new JournalAbility("Dégâts", "$bullet;[Onde] touche tous les joueurs.", false)]);
        Assert.Null(TrinketPlanner.Hit(boss, Healer.BossHits));
    }

    private static GearPiece Trinket(string effect) =>
        new(GearSlot.Trinket, 42, "Bijou", 700, [], [], [new ItemEffect(1, "Effet", effect)], false);
}
