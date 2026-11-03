using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Noovio.FrontPulse.Application.Meta;
using Noovio.FrontPulse.Domain.Entities;
using Noovio.FrontPulse.Domain.Enums;

namespace Noovio.FrontPulse.Application.Tests;

public sealed class TankMetaAggregatorServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly TankMetaAggregatorService _aggregator = new(Options.Create(new MetaOptions()));

    [Fact]
    public void Median_is_robust_to_outliers()
    {
        Assert.Equal(25, TankMetaAggregatorService.Median([10, 20, 30, 9000]));
        Assert.Equal(3, TankMetaAggregatorService.Median([5, 1, 3]));
        Assert.Equal(0, TankMetaAggregatorService.Median([]));
    }

    [Fact]
    public void Item_share_counts_each_tank_once_per_item()
    {
        // Deux anneaux identiques sur le premier tank : 1 tank sur 2, pas 2 sur 2.
        var profiles = new[]
        {
            Profile(1, Gear(GearSlot.Finger, 100), Gear(GearSlot.Finger, 100)),
            Profile(2, Gear(GearSlot.Finger, 200), Gear(GearSlot.Finger, 300))
        };

        var rings = Snapshot(profiles).Slots.Single(s => s.Slot == GearSlot.Finger).Items;

        Assert.Equal(0.5, rings.Single(i => i.ItemId == 100).Share);
        Assert.Equal(3, rings.Count);
    }

    [Fact]
    public void Trinkets_are_split_by_orientation()
    {
        var shield = Gear(GearSlot.Trinket, 1, effect: "Absorbs 500,000 damage for 10 sec.");
        var damage = Gear(GearSlot.Trinket, 2, effect: "Deal 300,000 Fire damage to your target.");

        var snapshot = Snapshot([Profile(1, shield, damage)]);

        Assert.Equal([1], snapshot.DefensiveTrinkets.Select(t => t.ItemId));
        Assert.Equal([2], snapshot.OffensiveTrinkets.Select(t => t.ItemId));
    }

    [Fact]
    public void Configured_trinket_ids_override_keywords()
    {
        var aggregator = new TankMetaAggregatorService(Options.Create(new MetaOptions { OffensiveTrinketIds = [1] }));

        Assert.Equal(TrinketOrientation.Offensive, aggregator.Orientation(Gear(GearSlot.Trinket, 1, effect: "Heal yourself")));
    }

    [Fact]
    public void Stat_shares_are_medians_normalised_to_one()
    {
        var profiles = new[]
        {
            Profile(1),
            Profile(2),
            Profile(3)
        };
        profiles[0].Stats = Sheet(haste: 4000, vers: 2000, mastery: 1000, crit: 1000);
        profiles[1].Stats = Sheet(haste: 5000, vers: 2000, mastery: 2000, crit: 1000);
        profiles[2].Stats = Sheet(haste: 30000, vers: 0, mastery: 0, crit: 0); // valeur extrême

        var stats = Snapshot(profiles).Stats;

        Assert.Equal(SecondaryStat.Haste, stats[0].Stat);
        Assert.Equal(5000, stats[0].MedianRating);
        Assert.Equal(1, stats.Sum(s => s.MedianShare), precision: 6);
        // Hâte en % : 40, 50, 300 → quartiles 45 et 175 encadrant la médiane 50.
        Assert.Equal((45, 50, 175), (stats[0].P25Percent, stats[0].MedianPercent, stats[0].P75Percent));
    }

    [Fact]
    public void Talent_builds_and_embellishments_are_ranked_by_popularity()
    {
        var embellished = Gear(GearSlot.Wrist, 10, effect: "Writhing Armor Banding", embellished: true);
        var profiles = new[]
        {
            Profile(1, embellished),
            Profile(2, embellished),
            Profile(3)
        };
        profiles[0].LoadoutCode = "AAA";
        profiles[1].LoadoutCode = "AAA";
        profiles[2].LoadoutCode = "BBB";

        var snapshot = Snapshot(profiles);

        Assert.Equal("AAA", snapshot.TalentBuilds[0].LoadoutCode);
        Assert.Equal(2.0 / 3, snapshot.TalentBuilds[0].Share, precision: 6);
        Assert.Equal(["Writhing Armor Banding"], snapshot.Embellishments.Single().Parts.Select(p => p.Name));
    }

    [Fact]
    public void Same_embellishment_on_two_pieces_is_counted_once_with_multiplier()
    {
        var wrist = Gear(GearSlot.Wrist, 10, effect: "Doublure en tisse-arcane", embellished: true);
        var back = Gear(GearSlot.Back, 11, effect: "Doublure en tisse-arcane", embellished: true);

        var part = Snapshot([Profile(1, wrist, back)]).Embellishments.Single().Parts.Single();

        Assert.Equal(("Doublure en tisse-arcane", 2), (part.Name, part.Count));
    }

    [Fact]
    public void Embellishment_falls_back_to_item_icon_when_spell_has_none()
    {
        var snapshot = Snapshot([Profile(1, Gear(GearSlot.Wrist, 10, effect: "Doublure en tisse-arcane", embellished: true))]);

        var withIcons = TankMetaAggregatorService.WithIcons(snapshot, new Dictionary<int, string> { [10] = "wrist.jpg" }, new Dictionary<int, string>());

        Assert.Equal("wrist.jpg", withIcons.Embellishments[0].Parts[0].IconUrl);
    }

    [Fact]
    public void Icons_are_applied_to_slots_trinkets_and_embellishments()
    {
        var embellished = Gear(GearSlot.Wrist, 9, effect: "Writhing Armor Banding", embellished: true);
        var snapshot = Snapshot([Profile(1, Gear(GearSlot.Head, 7), Gear(GearSlot.Trinket, 8), embellished)]);

        var withIcons = TankMetaAggregatorService.WithIcons(
            snapshot,
            new Dictionary<int, string> { [7] = "head.jpg", [8] = "trinket.jpg" },
            new Dictionary<int, string> { [1] = "spell.jpg" });

        Assert.Equal("head.jpg", withIcons.Slots.Single(s => s.Slot == GearSlot.Head).Items[0].IconUrl);
        Assert.Equal("trinket.jpg", withIcons.OffensiveTrinkets[0].IconUrl);
        Assert.Equal("spell.jpg", withIcons.Embellishments[0].Parts[0].IconUrl);
        Assert.Equal([1], TankMetaAggregatorService.SpellIds(snapshot));
    }

    [Fact]
    public void Talent_tree_receives_pick_rates_and_drops_unplayed_hero_trees()
    {
        var p1 = Profile(1);
        var p2 = Profile(2);
        p1.HeroTree = p2.HeroTree = "San'layn";
        p1.Talents = [new TalentPick(TalentTree.Spec, 10, 100, "Iron Skin", 1), new TalentPick(TalentTree.Hero, 20, 200, "Vampiric Strike", 1)];
        p2.Talents = [new TalentPick(TalentTree.Spec, 10, 100, "Iron Skin", 1)];
        var layout = new TalentTreeLayout(
        [
            Tree(TalentTree.Spec, "Sang", 10),
            Tree(TalentTree.Hero, "Porte-mort", 30),
            Tree(TalentTree.Hero, "San'layn", 20)
        ]);

        var tree = TankMetaAggregatorService.WithTalentTree(Snapshot([p1, p2]), layout).TalentTree!;

        Assert.Equal(1, tree.Sections.Single(s => s.Tree == TalentTree.Spec).Nodes[0].Choices[0].Share);
        var hero = Assert.Single(tree.Sections, s => s.Tree == TalentTree.Hero);
        Assert.Equal("San'layn", hero.Name);
        Assert.Equal(0.5, hero.Nodes[0].Choices[0].Share);
    }

    private static TalentTreeSection Tree(TalentTree kind, string name, int talentId) => new(
        kind, name, null, 0, 1, 1,
        [new TalentNodeView(1, 0, 0, TalentNodeKind.Active, 1, [], [new TalentChoiceView(talentId, 0, "", null, 0)])]);

    [Fact]
    public void Snapshot_round_trips_through_json()
    {
        // Même réglages que la colonne jsonb : les records à listes en lecture seule doivent se relire.
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        var profile = Profile(1, Gear(GearSlot.Trinket, 8, effect: "Absorbs damage"));
        profile.Stats = Sheet(1000, 500, 500, 200);
        profile.Talents = [new TalentPick(TalentTree.Spec, 42, 4242, "Iron Skin", 1)];
        var snapshot = Snapshot([profile]);

        var copy = JsonSerializer.Deserialize<SpecMetaSnapshot>(JsonSerializer.Serialize(snapshot, options), options)!;

        Assert.Equal(snapshot.Spec, copy.Spec);
        Assert.Equal(snapshot.DefensiveTrinkets[0], copy.DefensiveTrinkets[0]);
        Assert.Equal(snapshot.Talents[0], copy.Talents[0]);
        Assert.Equal(snapshot.Stats.Count, copy.Stats.Count);
    }

    private SpecMetaSnapshot Snapshot(IReadOnlyList<TankProfile> profiles) =>
        _aggregator.Aggregate(TankSpec.BrewmasterMonk, "season-mn-2", profiles, Now);

    private static TankProfile Profile(int rank, params GearPiece[] gear) => new()
    {
        Season = "season-mn-2",
        Spec = TankSpec.BrewmasterMonk,
        Rank = rank,
        Name = $"Tank{rank}",
        Region = "eu",
        RealmSlug = "hyjal",
        BestKeyLevel = 20 + rank,
        ItemLevel = 670 + rank,
        Gear = [.. gear]
    };

    private static GearPiece Gear(GearSlot slot, int itemId, string? effect = null, bool embellished = false) => new(
        slot,
        itemId,
        $"Item {itemId}",
        680,
        [],
        [],
        effect is null ? [] : [new ItemEffect(1, effect, effect)],
        embellished);

    private static StatSheet Sheet(double haste, double vers, double mastery, double crit) => new(
        new(haste, haste / 100), new(vers, vers / 100), new(mastery, mastery / 100), new(crit, crit / 100));
}
