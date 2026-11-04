using Microsoft.Extensions.Options;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Analysis;
using Noovio.Pulse.Application.Meta;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Enums;
using Noovio.Pulse.Domain.Roles;

namespace Noovio.Pulse.Tests;

public sealed class GearPlanTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private const int MasteryGem = 100;
    private const int HasteGem = 101;
    private const int MetaHead = 10;
    private const int MyHead = 11;
    private const int MetaTrinket = 20;
    private const int MyTrinket = 21;

    // --- Lecture des textes Blizzard -----------------------------------------

    [Theory]
    [InlineData("+70 Hâte", SecondaryStat.Haste, 70)]
    [InlineData("+16 à la Hâte", SecondaryStat.Haste, 16)]
    [InlineData("+7 au score de Coup critique", SecondaryStat.Crit, 7)]
    [InlineData("+147 Maîtrise", SecondaryStat.Mastery, 147)]
    [InlineData("Enchanted: +147 Versatility", SecondaryStat.Versatility, 147)]
    [InlineData("+1 200 Coup critique", SecondaryStat.Crit, 1200)]
    [InlineData("Enchanté : +30 Polyvalence |A:Professions-ChatIcon-Quality-Tier3:20:20|a", SecondaryStat.Versatility, 30)]
    public void Stat_text_is_parsed_in_french_and_english(string text, SecondaryStat stat, int value)
    {
        Assert.Equal([new ItemStat(stat, value)], StatText.Parse(text));
    }

    [Fact]
    public void Hybrid_gems_give_both_stats_and_other_bonuses_are_ignored()
    {
        Assert.Equal(
            [new ItemStat(SecondaryStat.Haste, 70), new ItemStat(SecondaryStat.Mastery, 30)],
            StatText.Parse("+70 Hâte et +30 Maîtrise"));
        Assert.Equal(
            [new ItemStat(SecondaryStat.Haste, 16), new ItemStat(SecondaryStat.Crit, 7)],
            StatText.Parse("+16 à la Hâte et +7 au score de Coup critique"));
        Assert.Empty(StatText.Parse("+32 à la caractéristique principale"));
        Assert.Empty(StatText.Parse("+41 à l’agilité/force et +115 à l’endurance"));
        Assert.Empty(StatText.Parse("+200 Endurance"));
        Assert.Empty(StatText.Parse("Effet spécial"));
    }

    [Fact]
    public void Enchant_labels_lose_their_prefix_and_quality_marker()
    {
        Assert.Equal("Vitesse des vents", StatText.ShortLabel("Enchanté : Vitesse des vents |A:Professions-ChatIcon-Quality-Tier2:20:20|a"));
    }

    // --- Gemmes et enchantements ----------------------------------------------

    [Fact]
    public void Off_priority_gems_are_swapped_towards_the_stat_below_the_top_range()
    {
        var top = Top();
        var mine = Player(top, Sheet(mastery: 35, haste: 45), [Head(MetaHead, Gem(HasteGem, "Gemme de hâte", "+100 Hâte"))]);

        var review = Analyze(top, mine).GemsAndEnchants;

        var swap = Assert.Single(review.Swaps);
        Assert.Equal(SwapKind.Gem, swap.Kind);
        Assert.Equal("Gemme de maîtrise", swap.To);
        Assert.Equal("Gemme de hâte", swap.From);
        Assert.Contains(swap.Deltas, d => d.Stat == SecondaryStat.Mastery && d.Percent > 0);
        Assert.Contains(swap.Deltas, d => d.Stat == SecondaryStat.Haste && d.Percent < 0);

        var mastery = review.Projection.Single(p => p.Stat == SecondaryStat.Mastery);
        Assert.True(mastery.Projected > mastery.Current);
        Assert.Equal(Verdict.Improve, review.Sockets.Single().Verdict);
    }

    [Fact]
    public void Gems_are_left_alone_when_stats_are_already_in_range()
    {
        var top = Top();
        var mine = Player(top, Sheet(mastery: 45, haste: 30), [Head(MetaHead, Gem(MasteryGem, "Gemme de maîtrise", "+100 Maîtrise"))]);

        var analysis = Analyze(top, mine);

        Assert.Empty(analysis.GemsAndEnchants.Swaps);
        Assert.Equal(Verdict.Good, analysis.Areas.Single(a => a.Kind == AnalysisAreaKind.Gems).Verdict);
    }

    [Fact]
    public void An_empty_socket_is_a_problem_and_gets_filled_with_a_gem_of_the_top()
    {
        var top = Top();
        var mine = Player(top, Sheet(mastery: 45, haste: 30), [Head(MetaHead, new GemSocket("PRISMATIC", null, null, null))]);

        var analysis = Analyze(top, mine);

        Assert.Equal(Verdict.Problem, analysis.Areas.Single(a => a.Kind == AnalysisAreaKind.Gems).Verdict);
        var swap = Assert.Single(analysis.GemsAndEnchants.Swaps);
        Assert.Equal(SwapKind.FillSocket, swap.Kind);
        Assert.Equal("Gemme de maîtrise", swap.To);
        Assert.Contains(analysis.Findings, f => f.Area == AnalysisAreaKind.Gems && f.Verdict == Verdict.Problem && f.How.Contains("Gemme de maîtrise"));
    }

    [Fact]
    public void A_gem_with_a_special_effect_is_never_replaced()
    {
        var top = Top();
        var mine = Player(top, Sheet(mastery: 35, haste: 45), [Head(MetaHead, Gem(999, "Diamant à effet", "Chance de déclencher un effet"))]);

        Assert.Empty(Analyze(top, mine).GemsAndEnchants.Swaps);
    }

    [Fact]
    public void Slot_with_a_socket_in_the_top_but_not_on_the_player_is_reported()
    {
        var top = Top();
        var mine = Player(top, Sheet(mastery: 45, haste: 30), [Head(MetaHead)]);

        var review = Analyze(top, mine).GemsAndEnchants;

        Assert.Equal([GearSlot.Head], review.MissingSockets);
    }

    [Fact]
    public void Profiles_ingested_before_sockets_existed_do_not_break_the_review()
    {
        var top = Top().Select(p =>
        {
            p.Gear = p.Gear.Select(g => g with { Sockets = null, Stats = null }).ToList();
            return p;
        }).ToList();

        var review = Analyze(top, Player(top, Sheet(mastery: 45, haste: 30), [Head(MetaHead, Gem(HasteGem, "Gemme de hâte", "+100 Hâte"))])).GemsAndEnchants;

        Assert.False(review.TopHasDetails);
        Assert.Empty(review.Swaps);
        Assert.Equal(Verdict.Good, review.Sockets.Single().Verdict);
    }

    [Fact]
    public void Item_gem_and_enchant_ratings_are_split_by_origin()
    {
        var top = Top();
        var piece = Head(MetaHead, Gem(MasteryGem, "Gemme de maîtrise", "+100 Maîtrise")) with
        {
            Enchantments = ["Enchanté : +50 Maîtrise"],
            Stats = [new ItemStat(SecondaryStat.Mastery, 500)]
        };

        var mastery = Analyze(top, Player(top, Sheet(mastery: 45, haste: 30), [piece])).GemsAndEnchants.Breakdown.Single(b => b.Stat == SecondaryStat.Mastery);

        Assert.Equal((500, 100, 50), (mastery.Items, mastery.Gems, mastery.Enchants));
        Assert.Equal(100, mastery.TopGems);
    }

    [Fact]
    public void Gear_stored_before_sockets_existed_still_deserializes()
    {
        const string stored = """{"slot":"Head","itemId":1,"name":"Casque","itemLevel":300,"bonusIds":[],"enchantments":[],"effects":[],"isEmbellished":false}""";

        var piece = System.Text.Json.JsonSerializer.Deserialize<GearPiece>(stored, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        })!;

        Assert.Null(piece.Sockets);
        Assert.Null(piece.Stats);
    }

    [Fact]
    public void The_most_common_enchant_is_suggested_without_its_quality_marker()
    {
        var top = Top().Select(p =>
        {
            p.Gear = [.. p.Gear, new GearPiece(GearSlot.Back, 50, "Cape", 300, [], ["Enchanté : Vents |A:Professions-Tier3:20:20|a"], [], false)];
            return p;
        }).ToList();
        var mine = Player(top, Sheet(mastery: 45, haste: 30), [new GearPiece(GearSlot.Back, 50, "Cape", 300, [], [], [], false)]);

        var finding = Assert.Single(Analyze(top, mine).Findings, f => f.Area == AnalysisAreaKind.Enchantments);

        Assert.Contains("Enchanté : Vents", finding.How);
        Assert.DoesNotContain("|A:", finding.How);
    }

    // --- Roadmap ---------------------------------------------------------------

    [Fact]
    public void Roadmap_groups_targets_by_instance_with_their_boss_trinkets_first()
    {
        var top = Top();
        var mine = Player(top, Sheet(mastery: 45, haste: 30), [Head(MyHead), Trinket(MyTrinket, "Bijou exotique")]);

        var roadmap = Analyze(top, mine, Journal()).Roadmap;

        Assert.Equal(["Salles du bijou", "Crypte des casques"], roadmap.Stops.Select(s => s.Instance));
        var trinket = Assert.Single(roadmap.Stops[0].Targets);
        Assert.Equal((MetaTrinket, "Gardien du bijou"), (trinket.ItemId, trinket.Boss));
        Assert.Equal("Bijou exotique", trinket.CurrentName);
    }

    [Fact]
    public void A_target_that_raises_a_low_stat_says_so()
    {
        var top = Top();
        var mine = Player(top, Sheet(mastery: 35, haste: 30), [Head(MyHead)]);

        var target = Analyze(top, mine, Journal()).Roadmap.Stops.SelectMany(s => s.Targets).Single(t => t.ItemId == MetaHead);

        Assert.Equal([SecondaryStat.Mastery], target.Fixes);
    }

    [Fact]
    public void A_dungeon_never_done_is_flagged_on_the_stop()
    {
        var top = Top();
        var mine = Player(top, Sheet(mastery: 45, haste: 30), [Head(MyHead)]) with
        {
            MythicPlus = new MythicPlusProfile(1000, null, 1000, null, null, null, [], ["Crypt of Helms"])
        };

        var stop = Assert.Single(Analyze(top, mine, Journal()).Roadmap.Stops);

        Assert.True(stop.KeyIsMissing);
    }

    [Fact]
    public void The_right_piece_too_low_is_an_upgrade_not_a_replacement()
    {
        var top = Top();
        var mine = Player(top, Sheet(mastery: 45, haste: 30), [Head(MetaHead) with { ItemLevel = 280 }]);

        var roadmap = Analyze(top, mine, Journal()).Roadmap;

        Assert.Empty(roadmap.Stops);
        var upgrade = Assert.Single(roadmap.Upgrades);
        Assert.Equal((280, 300), (upgrade.CurrentItemLevel, upgrade.TargetItemLevel));
    }

    [Fact]
    public void Without_a_journal_targets_are_kept_without_a_source()
    {
        var top = Top();
        var roadmap = Analyze(top, Player(top, Sheet(mastery: 45, haste: 30), [Head(MyHead)])).Roadmap;

        Assert.False(roadmap.HasJournal);
        Assert.Empty(roadmap.Stops);
        Assert.Equal(MetaHead, Assert.Single(roadmap.Elsewhere).ItemId);
    }

    [Fact]
    public void Player_with_the_top_gear_has_nothing_to_farm()
    {
        var top = Top();
        var roadmap = Analyze(top, Player(top, Sheet(mastery: 45, haste: 30), [Head(MetaHead), Trinket(MetaTrinket, "Bijou du top")]), Journal()).Roadmap;

        Assert.Empty(roadmap.Stops);
        Assert.Empty(roadmap.Elsewhere);
        Assert.Empty(roadmap.Upgrades);
    }

    // --- Bijoux -------------------------------------------------------------------

    [Theory]
    [InlineData("$bullet; [Faucheuse caudale] inflige d’importants dégâts physiques.", true)]
    [InlineData("$bullet;[Battement de queue] inflige des dégâts physiques à la cible actuelle.", true)]
    [InlineData("$bullet; Les attaques du second Nama contre sa cible infligent des dégâts physiques croissants.", true)]
    [InlineData("$bullet; [Rugissement dévorant] infligent d’importants dégâts à tous les personnages-joueurs.", false)]
    [InlineData("$bullet; Avec [Débarouler], elle charge et inflige d’importants dégâts physiques répartis entre les personnages-joueurs touchés.", false)]
    [InlineData("$bullet; [Enfermer] emprisonne le personnage-joueur ciblé dans l’une des cryptes.", false)]
    [InlineData("$bullet; Rav’i inflige des dégâts de mêlée à l’aide de [Frappe de l’hydre].", false)]
    public void Boss_hits_come_from_the_bullets_of_the_tank_section(string text, bool flagged)
    {
        var hit = TrinketPlanner.Hit(new JournalBoss(1, "Boss", [], [new JournalAbility("Tanks", text, true)]), PulseRoles.Tank.BossHits);

        Assert.Equal(flagged, hit is not null);
    }

    [Fact]
    public void A_section_outside_the_tank_heading_is_ignored_and_the_hardest_bullet_is_quoted()
    {
        var boss = new JournalBoss(1, "Boss", [],
        [
            new JournalAbility("Vue d’ensemble", "$bullet; [Autre] inflige d’importants dégâts physiques.", false),
            new JournalAbility("Tank", "$bullet; [Gifle] inflige des dégâts physiques à la cible actuelle.\r\n$bullet; [Fente] inflige d’importants dégâts physiques à la cible actuelle.", true)
        ]);

        var hit = TrinketPlanner.Hit(boss, PulseRoles.Tank.BossHits)!;

        Assert.Equal("Fente", hit.Ability);
        Assert.Equal("Fente inflige d’importants dégâts physiques à la cible actuelle.", hit.Text);
    }

    [Fact]
    public void Trinket_usage_comes_from_the_effect_text()
    {
        var top = Top();
        var mine = Player(top, Sheet(mastery: 45, haste: 30), [
            Trinket(MyTrinket, "Égide", new ItemEffect(1, "Égide", "Utiliser : Absorbe 1000 points de dégâts. (1 min 30 s de recharge)\r\n\r\nSecond paragraphe.")),
            Trinket(MetaTrinket, "Talisman", new ItemEffect(2, "Talisman", "Équipé : Vos attaques ont une chance de conférer de la Hâte."))
        ]);

        var trinkets = Analyze(top, mine).TrinketPlan.Trinkets;

        var aegis = trinkets.Single(t => t.Name == "Égide").Usage;
        Assert.True(aegis.OnUse);
        Assert.Equal(TrinketOrientation.Role, aegis.Orientation);
        Assert.Equal("1 min 30 s de recharge", aegis.Recharge);
        Assert.Equal("Absorbe 1000 points de dégâts.", aegis.Effect);
        Assert.False(trinkets.Single(t => t.Name == "Talisman").Usage.OnUse);
    }

    [Fact]
    public void A_rare_trinket_is_replaced_with_where_to_find_its_replacement()
    {
        var top = Top();
        var mine = Player(top, Sheet(mastery: 45, haste: 30), [Trinket(MyTrinket, "Bijou exotique")]);

        var advice = Assert.Single(Analyze(top, mine, Journal()).TrinketPlan.Trinkets);

        Assert.Equal(MetaTrinket, advice.ReplaceWith!.ItemId);
        Assert.Equal(("Gardien du bijou", "Salles du bijou", InstanceKind.Dungeon), (advice.ReplaceWith.Boss, advice.ReplaceWith.Instance, advice.ReplaceWith.Kind!.Value));
    }

    [Fact]
    public void A_trinket_of_the_top_is_kept()
    {
        var top = Top();
        var mine = Player(top, Sheet(mastery: 45, haste: 30), [Trinket(MetaTrinket, "Bijou du top")]);

        Assert.Null(Assert.Single(Analyze(top, mine).TrinketPlan.Trinkets).ReplaceWith);
    }

    [Fact]
    public void The_replacement_completes_the_pair_of_the_trinket_kept()
    {
        const int a = 40, b = 41, c = 42;
        var top = Enumerable.Range(1, 10).Select(rank => new PlayerProfile
        {
            Season = "season-mn-2", Spec = WowSpec.BloodDeathKnight, Rank = rank, Name = $"T{rank}", ItemLevel = 300, BestKeyLevel = 20,
            // A+B : 4 tanks, C+B : 6 tanks. C est plus portée que A, mais qui garde A vise B.
            Gear = rank <= 4 ? [Trinket(a, "A"), Trinket(b, "B")] : [Trinket(c, "C"), Trinket(b, "B")],
            Stats = Sheet(mastery: 45, haste: 30)
        }).ToList();
        var mine = Player(top, Sheet(mastery: 45, haste: 30), [Trinket(a, "A"), Trinket(MyTrinket, "Bijou exotique")]);

        var analysis = Analyze(top, mine);
        var plan = analysis.TrinketPlan;

        Assert.Equal(b, plan.Trinkets.Single(t => t.ItemId == MyTrinket).ReplaceWith!.ItemId);
        Assert.Null(plan.Trinkets.Single(t => t.ItemId == a).ReplaceWith);
        Assert.Equal([0.6, 0.4], plan.Pairs.Select(p => p.Share));
        Assert.Equal(b, Assert.Single(analysis.Roadmap.Elsewhere).ItemId); // la roadmap vise le même bijou
    }

    [Fact]
    public void Tank_hits_list_only_bosses_with_a_described_hit_dungeons_before_raids()
    {
        var top = Top();
        var hits = Analyze(top, Player(top, Sheet(mastery: 45, haste: 30), [Head(MetaHead)]), Journal()).TrinketPlan.BossHits;

        Assert.Equal(InstanceKind.Dungeon, hits[0].Kind);
        Assert.Equal(InstanceKind.Raid, hits[^1].Kind);
        Assert.Equal(["Gardien du bijou", "Colosse"], hits.SelectMany(i => i.Bosses).Select(b => b.Boss));
    }

    // --- Jeux de données ---------------------------------------------------------

    private static CharacterAnalysis Analyze(IReadOnlyList<PlayerProfile> top, CharacterDetails player, EncounterJournal? journal = null)
    {
        var aggregator = new MetaAggregatorService(Options.Create(new MetaOptions()), PulseRoles.Tank);
        var meta = aggregator.Aggregate(WowSpec.BloodDeathKnight, "season-mn-2", top, Now);
        return CharacterAnalyzer.Analyze(PulseRoles.Tank, WowSpec.BloodDeathKnight.Info(), meta, top, player, new Dictionary<int, string>(), Now, journal, aggregator.Orientation);
    }

    private static EncounterJournal Journal() => new(
    [
        new JournalInstance(1, "Crypte des casques", "Crypt of Helms", InstanceKind.Dungeon,
            [new JournalBoss(10, "Roi des casques", [MetaHead], [new JournalAbility("Tank", "$bullet; [Pluie] inflige des dégâts de Feu à tous les joueurs.", true)])]),
        new JournalInstance(2, "Salles du bijou", "Halls of Trinkets", InstanceKind.Dungeon,
            [new JournalBoss(20, "Gardien du bijou", [MetaTrinket], [new JournalAbility("Tank", "$bullet; [Frappe] inflige des dégâts physiques à la cible actuelle.", true)])]),
        new JournalInstance(3, "Le Grand Raid", "The Great Raid", InstanceKind.Raid, [new JournalBoss(30, "Colosse", [], [new JournalAbility("Tanks", "$bullet; [Poing] inflige d’importants dégâts physiques.", true)])])
    ]);

    /// <summary>Dix tanks identiques : casque avec une gemme de maîtrise (500 d'indice), bijou méta ; maîtrise entre 41 et 50 %, hâte stable à 30 %.</summary>
    private static List<PlayerProfile> Top() => Enumerable.Range(1, 10).Select(rank => new PlayerProfile
    {
        Season = "season-mn-2",
        Spec = WowSpec.BloodDeathKnight,
        Rank = rank,
        Name = $"Tank{rank}",
        ItemLevel = 300,
        BestKeyLevel = 20,
        Gear =
        [
            Head(MetaHead, Gem(MasteryGem, "Gemme de maîtrise", "+100 Maîtrise")) with { Stats = [new ItemStat(SecondaryStat.Mastery, 500)] },
            Trinket(MetaTrinket, "Bijou du top")
        ],
        Stats = Sheet(mastery: 40 + rank, haste: 30)
    }).ToList();

    private static GearPiece Head(int itemId, params GemSocket[] sockets) =>
        new(GearSlot.Head, itemId, itemId == MetaHead ? "Casque du top" : "Casque exotique", 300, [], [], [], false, sockets, []);

    private static GearPiece Trinket(int itemId, string name, params ItemEffect[] effects) =>
        new(GearSlot.Trinket, itemId, name, 300, [], [], effects, false, [], []);

    private static GemSocket Gem(int id, string name, string display) => new("PRISMATIC", id, name, display);

    /// <summary>100 d'indice valent 1 % : la conversion mesurée sur le top est donc de 0,01 % par point.</summary>
    private static StatSheet Sheet(double mastery, double haste) => new(
        new StatValue(haste * 100, haste), new StatValue(500, 5), new StatValue(mastery * 100, mastery), new StatValue(2500, 25));

    private static CharacterDetails Player(IReadOnlyList<PlayerProfile> top, StatSheet stats, IReadOnlyList<GearPiece> gear) =>
        new("Moi", "eu", "hyjal", (int)WowSpec.BloodDeathKnight, "Sang",
            new CharacterLoadout((int)WowSpec.BloodDeathKnight, 300, gear, stats, [], "MOI", null),
            null, null, null, null);
}
