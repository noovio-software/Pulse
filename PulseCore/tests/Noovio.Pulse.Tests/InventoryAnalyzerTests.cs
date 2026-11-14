using System.Text;
using Noovio.Pulse.Application.Analysis;
using Noovio.Pulse.Application.Import;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Enums;
using Noovio.Pulse.Domain.Roles;

namespace Noovio.Pulse.Tests;

/// <summary>Sacs et Grande Chambre forte de l'export de l'addon : quoi équiper, quoi choisir, consommables.</summary>
public sealed class InventoryAnalyzerTests
{
    private const string Json = """
        {
          "v": 1, "kind": "character",
          "character": { "name": "Aelys", "realm": "Hyjal", "region": "eu", "class": 5, "level": 90, "spec": 258 },
          "gear": [
            { "slot": "HEAD", "id": 1001, "name": "Tier Hood", "ilvl": 700, "set_id": 1900 },
            { "slot": "LEGS", "id": 1002, "name": "Old Leggings", "ilvl": 680 },
            { "slot": "WAIST", "id": 1003, "name": "Embellished Belt", "ilvl": 690, "embellished": true },
            { "slot": "TRINKET_1", "id": 2001, "name": "Popular Idol", "ilvl": 700 },
            { "slot": "TRINKET_2", "id": 2002, "name": "Rare Charm", "ilvl": 705 }
          ],
          "bags": {
            "gear": [
              { "slot": "HEAD", "id": 1101, "name": "Plain Hood", "ilvl": 705 },
              { "slot": "LEGS", "id": 1102, "name": "Better Leggings", "ilvl": 694 },
              { "slot": "WAIST", "id": 1103, "name": "Plain Belt", "ilvl": 695 },
              { "slot": "TRINKET_1", "id": 2003, "name": "Meta Trinket", "ilvl": 698 },
              { "slot": "CHEST", "id": 1104, "name": "Plate Chest", "ilvl": 720, "usable": false }
            ],
            "consumables": [
              { "id": 3001, "name": "Flask of Power", "kind": "flask", "count": 2, "quality": 2, "current": true },
              { "id": 3002, "name": "Tempered Potion", "kind": "potion", "count": 2, "current": true, "use": "Increases your primary stat by 900 for 30 sec." },
              { "id": 3003, "name": "Healing Potion", "kind": "potion", "count": 4, "current": true, "use": "Restores 300000 health." },
              { "id": 3004, "name": "Old Steak", "kind": "food", "count": 10, "current": false, "use": "Restores health. If you spend at least 10 seconds eating you will become well fed." }
            ]
          },
          "vault": {
            "has_rewards": true, "can_claim": true,
            "activities": [
              { "type": 1, "index": 1, "threshold": 1, "progress": 8, "level": 12, "example_ilvl": 707,
                "items": [{ "slot": "TRINKET_1", "id": 2003, "name": "Meta Trinket", "ilvl": 711 }] },
              { "type": 3, "index": 1, "threshold": 2, "progress": 2, "level": 15, "example_ilvl": 701,
                "items": [{ "slot": "FEET", "id": 1201, "name": "Boots", "ilvl": 701 }] },
              { "type": 3, "index": 2, "threshold": 4, "progress": 2, "level": 0 }
            ]
          }
        }
        """;

    private static InventoryReview Review(string json = Json)
    {
        var import = CharacterImportParser.Parse("NP1:J:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json))).Import!;
        return InventoryAnalyzer.Analyze(import, Meta(), PulseRoles.Damage, new Dictionary<int, string>())!;
    }

    [Fact]
    public void Bag_pieces_that_beat_the_equipped_one_are_recommended()
    {
        var upgrades = Review().BagUpgrades;

        Assert.Contains(upgrades, o => o.ItemId == 1102 && o.Gain == 14);
        Assert.Contains(upgrades, o => o.ItemId == 2003 && o.TopShare > 0.5); // bijou de la méta, même un peu plus bas
    }

    [Fact]
    public void A_set_piece_an_embellishment_or_an_unusable_item_is_kept()
    {
        var upgrades = Review().BagUpgrades.Select(o => o.ItemId).ToList();

        Assert.DoesNotContain(1101, upgrades); // +5 mais la coiffe compte pour le set
        Assert.DoesNotContain(1103, upgrades); // +5 mais la ceinture porte l'embellissement
        Assert.DoesNotContain(1104, upgrades); // armure que la classe ne porte pas
    }

    [Fact]
    public void The_vault_recommends_a_single_choice()
    {
        var vault = Review().Vault!;

        Assert.True(vault.RewardsReady);
        Assert.Equal(2003, vault.Choices[0].ItemId);
        Assert.Single(vault.Choices, c => c.Recommended);
        Assert.Contains("Meta Trinket", vault.Advice);
        Assert.Equal(["Raid", "Donjons"], vault.Tracks.Select(t => t.Label));
    }

    [Fact]
    public void Without_rewards_the_vault_shows_the_next_step()
    {
        var vault = Review(Json.Replace("\"has_rewards\": true", "\"has_rewards\": false").Replace("\"items\"", "\"ignored\"")).Vault!;

        Assert.False(vault.RewardsReady);
        Assert.Contains("2/3 cases", vault.Advice);
        Assert.Contains("encore 2 boss", vault.Advice);
    }

    [Fact]
    public void Consumables_are_checked_by_kind()
    {
        var checks = Review().Consumables.ToDictionary(c => c.Kind);

        Assert.Equal(Verdict.Good, checks[ConsumableKind.Flask].Verdict);
        Assert.Equal(Verdict.Improve, checks[ConsumableKind.CombatPotion].Verdict); // 2 en stock : trop peu
        Assert.Equal((Verdict.Good, 3003), (checks[ConsumableKind.HealthPotion].Verdict, checks[ConsumableKind.HealthPotion].ItemId));
        Assert.Contains("ancienne extension", checks[ConsumableKind.Food].Advice);
        Assert.Equal((Verdict.Improve, 0), (checks[ConsumableKind.WeaponBuff].Verdict, checks[ConsumableKind.WeaponBuff].Count));
        Assert.False(checks.ContainsKey(ConsumableKind.ManaPotion)); // pas soigneur
    }

    /// <summary>
    /// Comparées au maximum de leur piste : des bottes Héros 1/6 à 320 (336 au max) battent des bottes Champion 6/6 à 324,
    /// mais des gants Champion 6/6 à 334 ne battent pas des gants Héros 3/6 à 330 (336 au max).
    /// Le coffre ne donne pas le maximum : il est repris d'une pièce de la même piste.
    /// </summary>
    [Fact]
    public void Pieces_are_compared_at_their_max_upgrade_level()
    {
        const string json = """
            {
              "v": 1, "character": { "name": "A", "class": 5, "spec": 258 },
              "gear": [
                { "slot": "FEET", "id": 1, "name": "Champion Boots", "ilvl": 324, "upgrade": { "track": "Champion", "rank": 6, "max_rank": 6 } },
                { "slot": "HANDS", "id": 2, "name": "Hero Gloves", "ilvl": 330, "upgrade": { "track": "Héros", "rank": 3, "max_rank": 6, "max_ilvl": 336 } }
              ],
              "bags": { "gear": [
                { "slot": "FEET", "id": 3, "name": "Hero Boots", "ilvl": 320, "upgrade": { "track": "Héros", "rank": 1, "max_rank": 6, "max_ilvl": 336 } },
                { "slot": "HANDS", "id": 4, "name": "Champion Gloves", "ilvl": 334, "upgrade": { "track": "Champion", "rank": 6, "max_rank": 6 } }
              ] },
              "vault": { "has_rewards": true, "activities": [
                { "type": 1, "index": 1, "threshold": 1, "progress": 1, "level": 10,
                  "items": [{ "slot": "FEET", "id": 5, "name": "Vault Boots", "ilvl": 327, "upgrade": { "track": "Héros", "rank": 2, "max_rank": 6 } }] }
              ] }
            }
            """;

        var review = Review(json);

        var boots = Assert.Single(review.BagUpgrades);
        Assert.Equal((3, 320, 336, 12), (boots.ItemId, boots.ItemLevel, boots.MaxItemLevel, boots.Gain));
        Assert.Equal("Héros 6/6", boots.Upgrade);

        var vaultBoots = review.Vault!.Choices.Single();
        Assert.Equal((336, false, true), (vaultBoots.MaxItemLevel, vaultBoots.MaxEstimated, vaultBoots.Recommended));
        Assert.Contains("336 au max", review.Vault.Advice);
    }

    [Fact]
    public void Without_a_known_max_the_step_is_estimated()
    {
        var scale = UpgradeScale.From([]);

        var max = scale.Max(new ImportedGear("FEET", 1, "Boots", 320, null, null, null, null, null, null, false,
            Upgrade: new ImportedUpgrade("Aventurier", 2, 6, null)));

        Assert.Equal((332, true), (max.Level, max.Estimated));
    }

    [Fact]
    public void An_export_without_bags_nor_vault_has_no_review()
    {
        var import = CharacterImportParser.Parse("NP1:J:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(
            """{"v":1,"character":{"name":"A","class":5,"spec":258},"gear":[{"slot":"HEAD","id":1,"ilvl":1}]}"""))).Import!;

        Assert.Null(InventoryAnalyzer.Analyze(import, Meta(), PulseRoles.Damage, new Dictionary<int, string>()));
    }

    private static SpecMetaSnapshot Meta() => new(
        WowSpec.ShadowPriest, "season-mn-1", DateTimeOffset.UnixEpoch, 100, 705, 15,
        [],
        [new SlotUsage(GearSlot.Trinket, [Item(2003, 0.6), Item(2001, 0.4), Item(2002, 0.02)])],
        [], [], [], [], [], [], []);

    private static ItemStatSnapshot Item(int id, double share) => new(id, $"#{id}", null, (int)(share * 100), share, 700);
}
