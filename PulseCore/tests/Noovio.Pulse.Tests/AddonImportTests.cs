using System.IO.Compression;
using System.Text;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Import;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Enums;
using Noovio.Pulse.Domain.Roles;

namespace Noovio.Pulse.Tests;

/// <summary>Lecture de la chaîne de l'addon NoovioPulse et traduction dans le modèle de l'analyse.</summary>
public sealed class AddonImportTests
{
    private static readonly SpecInfo Shadow = WowSpec.ShadowPriest.Info();

    /// <summary>Un export tel que l'écrit l'addon (client anglais), réduit à l'essentiel.</summary>
    private const string Json = """
        {
          "v": 1, "addon": "1.0.0", "game": "12.0.5.60000", "locale": "enUS", "exported_at": 1794650000,
          "character": { "name": "Aelys", "realm": "Conseil des Ombres", "region": "eu", "class": 5, "level": 90,
                         "spec": 258, "spec_name": "Shadow", "ilvl": 701.4 },
          "gear": [
            { "slot": "HEAD", "id": 1001, "name": "Hood of Whispers", "ilvl": 710, "bonus": [1, 2], "embellished": false,
              "gems": [{ "id": 2001, "name": "Masterful Gem", "text": "+147 Mastery" }], "effects": [],
              "stats": { "MASTERY_RATING": 600, "HASTE_RATING": 300 } },
            { "slot": "CHEST", "id": 1002, "name": "Robe of Night", "ilvl": 700, "bonus": [], "embellished": true,
              "enchant": "Enchanted: +80 Intellect", "gems": [], "effects": [{ "trigger": "equip", "spell": 0, "text": "Your spells sometimes..." }],
              "stats": {} },
            { "slot": "TRINKET_1", "id": 1003, "name": "Idol", "ilvl": 707, "embellished": false, "gems": [],
              "effects": [{ "trigger": "use", "spell": 5001, "name": "Dark Pact", "text": "Gain power. (2 Min Cooldown)" }], "stats": {} },
            { "slot": "BODY", "id": 1004, "name": "Shirt", "ilvl": 1 },
            { "slot": "MAIN_HAND", "id": 1005, "name": "Staff", "ilvl": 700, "enchant_id": 7000 }
          ],
          "talents": { "code": "CODE123", "hero": "Voidweaver", "hero_id": 9,
                       "nodes": [[10, 100, 3001, 1, 0], [11, 110, 3012, 2, 0], [12, 120, 3020, 1, 1], [13, 130, 3030, 1, 1], [20, 200, 4001, 1, 0], [99, 990, 9999, 1, 0]] },
          "stats": { "haste": { "rating": 900, "melee": 10, "ranged": 11, "spell": 22.5 },
                     "crit": { "rating": 500, "melee": 12, "ranged": 13, "spell": 18.25 },
                     "mastery": { "rating": 700, "percent": 40 },
                     "versatility": { "rating": 300, "done": 6, "taken": 3 } },
          "sheet": { "health": 800000, "strength": 900, "agility": 1000, "stamina": 50000, "intellect": 40000, "mana": 250000,
                     "armor": 5000, "dodge": 3, "parry": 0, "block": 0, "leech": 1, "avoidance": 2, "speed": 4 }
        }
        """;

    [Fact]
    public void A_plain_export_is_read()
    {
        var result = CharacterImportParser.Parse("NP1:J:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(Json)));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("Aelys", result.Import!.Character!.Name);
        Assert.Equal(5, result.Import.Gear!.Count);
        Assert.Equal(new DateTimeOffset(2026, 11, 14, 9, 53, 20, TimeSpan.Zero), result.Import.ExportedAtUtc);
    }

    [Theory]
    [InlineData("deflate")]
    [InlineData("zlib")]
    [InlineData("gzip")]
    public void A_compressed_export_is_read_whatever_the_header(string format)
    {
        var text = "NP1:D:" + Convert.ToBase64String(Compress(Encoding.UTF8.GetBytes(Json), format));

        Assert.True(CharacterImportParser.Parse(text).Succeeded);
    }

    /// <summary>Le collage ajoute souvent des retours à la ligne ; le remplissage Base64 peut aussi sauter.</summary>
    [Fact]
    public void Whitespace_and_missing_padding_are_tolerated()
    {
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(Json)).TrimEnd('=');
        var wrapped = string.Join("\r\n", base64.Chunk(76).Select(c => new string(c)));

        Assert.True(CharacterImportParser.Parse("  NP1:J:\n" + wrapped + "\n").Succeeded);
    }

    [Theory]
    [InlineData("", "Colle la chaîne")]
    [InlineData("priest=\"Aelys\"\nlevel=90\nspec=shadow", "SimulationCraft")]
    [InlineData("NP1:J:%%%%", "tronquée")]
    [InlineData("NP1:X:AAAA", "illisible")]
    [InlineData("NP9:J:AAAA", "Version")]
    public void Wrong_strings_are_explained(string text, string expected)
    {
        var result = CharacterImportParser.Parse(text);

        Assert.False(result.Succeeded);
        Assert.Contains(expected, result.Error);
    }

    [Fact]
    public void An_export_from_a_newer_addon_is_refused()
    {
        var result = CharacterImportParser.Parse("NP1:J:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"v":2}""")));

        Assert.Contains("plus récente", result.Error);
    }

    [Fact]
    public void An_alts_export_points_to_AltPulse()
    {
        var result = CharacterImportParser.Parse("NP1:J:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"v":1,"kind":"alts","characters":[]}""")));

        Assert.Contains("AltPulse", result.Error);
    }

    [Fact]
    public void Gear_keeps_the_analysed_slots_with_their_details()
    {
        var gear = Details().Loadout.Gear;

        Assert.Equal([GearSlot.Head, GearSlot.Chest, GearSlot.Trinket, GearSlot.MainHand], gear.Select(g => g.Slot)); // chemise ignorée
        var head = gear[0];
        Assert.Equal(new GemSocket("PRISMATIC", 2001, "Masterful Gem", "+147 Mastery"), Assert.Single(head.Sockets!));
        Assert.Equal([new ItemStat(SecondaryStat.Mastery, 600), new ItemStat(SecondaryStat.Haste, 300)], head.Stats!.OrderByDescending(s => s.Value));
        Assert.Equal(["Enchanted: +80 Intellect"], gear[1].Enchantments);
        Assert.Equal(["Enchantement #7000"], gear[3].Enchantments); // l'enchantement compte même sans texte
        Assert.Equal(GearRules.AverageItemLevel(gear), Details().Loadout.ItemLevel);
    }

    /// <summary>L'analyse repère les effets à l'emploi par « Use: » / « Utiliser : » : le préfixe est remis quelle que soit la langue.</summary>
    [Fact]
    public void Effects_get_a_prefix_the_analysis_understands()
    {
        var gear = Details().Loadout.Gear;

        Assert.Equal("Use: Gain power. (2 Min Cooldown)", gear[2].Effects.Single().Description);
        Assert.Equal("Dark Pact", gear[2].Effects.Single().Name);
        Assert.Equal("Robe of Night", gear[1].Effects.Single().Name); // effet sans nom : celui de la pièce
    }

    [Fact]
    public void Embellishments_take_the_name_used_by_the_meta()
    {
        var meta = Meta() with
        {
            Embellishments = [new EmbellishmentCombo([new EmbellishmentPart("Fil de l'ombre", 0, 1002, null)], 50, 0.5)]
        };

        var chest = Details(meta: meta).Loadout.Gear.Single(g => g.Slot == GearSlot.Chest);

        Assert.True(chest.IsEmbellished);
        Assert.Equal("Fil de l'ombre", chest.Effects[0].Name);
    }

    /// <summary>
    /// Nœuds retrouvés dans l'arbre : option par sort pour un nœud à choix, nom français de la méta, arbre héroïque déduit
    /// des talents. Un nœud inconnu est ignoré ; un nœud offert ne compte que si le top le compte.
    /// </summary>
    [Fact]
    public void Talents_are_mapped_through_the_tree_layout()
    {
        var loadout = Details().Loadout;

        Assert.Equal(
            [(TalentTree.Class, 1, "Mot de pouvoir : Bouclier", 1), (TalentTree.Spec, 12, "Choix B", 2), (TalentTree.Spec, 13, "Forme d'ombre", 1), (TalentTree.Hero, 20, "Toile du Vide", 1)],
            loadout.Talents.Select(t => (t.Tree, t.TalentId, t.Name, t.Rank)));
        Assert.Equal("Tisse-Vide", loadout.HeroTree);
        Assert.Equal("CODE123", loadout.LoadoutCode);
    }

    [Fact]
    public void Talents_are_left_out_when_comparing_another_spec()
    {
        var details = Details(spec: WowSpec.DisciplinePriest.Info());

        Assert.Empty(details.Loadout.Talents);
        Assert.Null(details.Loadout.HeroTree);
        Assert.Equal(258, details.ActiveSpecId);
    }

    [Fact]
    public void Stats_are_read_on_the_lines_of_the_spec()
    {
        var details = Details();

        Assert.Equal(new StatValue(900, 22.5), details.Loadout.Stats!.Haste);
        Assert.Equal(new StatValue(500, 18.25), details.Loadout.Stats.Crit);
        Assert.Equal(new StatValue(300, 6), details.Loadout.Stats.Versatility);
        Assert.Equal(3, details.Sheet!.VersatilityDamageTaken);
        Assert.Equal(0, details.Sheet.Mana); // le mana n'est affiché que pour un soigneur
    }

    [Theory]
    [InlineData("Conseil des Ombres", "conseil-des-ombres")]
    [InlineData("Chants éternels", "chants-eternels")]
    [InlineData("Arak-arahm", "arakarahm")]
    [InlineData("Pozzo dell'Eternità", "pozzo-delleternita")]
    [InlineData("Aggra (Português)", "aggra-portugues")]
    [InlineData("Hyjal", "hyjal")]
    public void Realm_names_become_blizzard_slugs(string realm, string slug) =>
        Assert.Equal(slug, ImportedCharacterMapper.RealmSlug(realm));

    [Fact]
    public void Identity_and_extras_are_kept()
    {
        var extras = new CharacterExtras("portrait.jpg", "avatar.jpg", null);
        var details = Details(extras: extras);

        Assert.Equal(("Aelys", "eu", "conseil-des-ombres", "Shadow"), (details.Name, details.Region, details.RealmSlug, details.ActiveSpecName));
        Assert.Equal("portrait.jpg", details.PortraitUrl);
    }

    private static CharacterDetails Details(SpecInfo? spec = null, SpecMetaSnapshot? meta = null, CharacterExtras? extras = null)
    {
        var import = CharacterImportParser.Parse("NP1:J:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(Json))).Import!;
        var snapshot = meta ?? Meta();
        return ImportedCharacterMapper.ToDetails(import, PulseRoles.Damage, spec ?? Shadow, snapshot, snapshot.TalentTree, extras ?? CharacterExtras.None);
    }

    private static SpecMetaSnapshot Meta() => new(
        WowSpec.ShadowPriest, "season-mn-1", DateTimeOffset.UnixEpoch, 100, 705, 15,
        [], [], [], [], [], [], [new HeroTreeUsage("Tisse-Vide", 80, 0.8)],
        // Le nœud 13 est offert et compté par le top ; le nœud 12, offert, ne l'est pas.
        [new TalentPickRate(TalentTree.Spec, 13, 3030, "Forme d'ombre", 1, 1)],
        [],
        new TalentTreeLayout(
        [
            Section(TalentTree.Class, "Prêtre", Node(10, Choice(1, 3001, "Mot de pouvoir : Bouclier"))),
            Section(TalentTree.Spec, "Ombre",
                Node(11, Choice(11, 3011, "Choix A"), Choice(12, 3012, "Choix B")),
                Node(12, Choice(14, 3020, "Offert inconnu du top")),
                Node(13, Choice(13, 3030, "Forme d'ombre"))),
            Section(TalentTree.Hero, "Tisse-Vide", Node(20, Choice(20, 4001, "Toile du Vide"))),
            Section(TalentTree.Hero, "Archon", Node(30, Choice(30, 5001, "Halo")))
        ]));

    private static TalentTreeSection Section(TalentTree tree, string name, params TalentNodeView[] nodes) =>
        new(tree, name, null, 1, 5, 5, nodes);

    private static TalentNodeView Node(int id, params TalentChoiceView[] choices) =>
        new(id, 0, 0, choices.Length > 1 ? TalentNodeKind.Choice : TalentNodeKind.Passive, 2, [], choices);

    private static TalentChoiceView Choice(int talentId, int spellId, string name) => new(talentId, spellId, name, null, 0);

    private static byte[] Compress(byte[] data, string format)
    {
        using var output = new MemoryStream();
        using (Stream stream = format switch
               {
                   "zlib" => new ZLibStream(output, CompressionLevel.Optimal),
                   "gzip" => new GZipStream(output, CompressionLevel.Optimal),
                   _ => new DeflateStream(output, CompressionLevel.Optimal)
               })
        {
            stream.Write(data);
        }

        return output.ToArray();
    }
}
