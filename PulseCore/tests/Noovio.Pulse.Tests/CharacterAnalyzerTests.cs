using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Analysis;
using Noovio.Pulse.Application.Meta;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Enums;
using Noovio.Pulse.Domain.Roles;
using Noovio.Pulse.Infrastructure.Persistence;
using Noovio.Pulse.Web.ShareCard;

namespace Noovio.Pulse.Tests;

public sealed class CharacterAnalyzerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly SpecInfo Blood = WowSpec.BloodDeathKnight.Info();

    [Fact]
    public void A_player_matching_the_top_scores_100()
    {
        var top = Top();
        var analysis = Analyze(top, Player(top[0]));

        Assert.Equal(100, analysis.Score);
        Assert.All(analysis.Areas, a => Assert.Equal(Verdict.Good, a.Verdict));
        Assert.All(analysis.Findings, f => Assert.Equal(Verdict.Good, f.Verdict));
    }

    [Fact]
    public void Missing_enchant_expected_by_the_top_is_a_problem()
    {
        var top = Top();
        var mine = Player(top[0], gear: top[0].Gear.Select(g => g.Slot == GearSlot.Chest ? g with { Enchantments = [] } : g).ToList());

        var analysis = Analyze(top, mine);

        Assert.Equal(Verdict.Problem, analysis.Areas.Single(a => a.Kind == AnalysisAreaKind.Enchantments).Verdict);
        var finding = Assert.Single(analysis.Findings, f => f.Area == AnalysisAreaKind.Enchantments && f.Verdict == Verdict.Problem);
        Assert.Contains("Enchanté : Endurance", finding.How);
        Assert.Equal(Verdict.Problem, analysis.Findings[0].Verdict); // les problèmes passent en tête du plan d'action
    }

    /// <summary>L'analyse est rédigée dans la langue de l'interface : un visiteur anglophone reçoit un plan d'action en anglais.</summary>
    [Fact]
    public void Findings_are_written_in_the_interface_language()
    {
        var top = Top();
        var mine = Player(top[0], gear: top[0].Gear.Select(g => g.Slot == GearSlot.Chest ? g with { Enchantments = [] } : g).ToList());

        using var _ = TestCulture.Use("en");
        var finding = Assert.Single(Analyze(top, mine).Findings, f => f.Area == AnalysisAreaKind.Enchantments && f.Verdict == Verdict.Problem);

        Assert.Equal("Chest without enchantment", finding.Title);
        Assert.StartsWith("Most common in the top:", finding.How);
    }

    [Fact]
    public void Core_talents_not_taken_are_listed_as_missing()
    {
        var top = Top();
        var mine = Player(top[0], talents: [new TalentPick(TalentTree.Spec, 99, 990, "Choix exotique", 1)]);

        var talents = Analyze(top, mine).Talents;

        Assert.Equal(["Peau de fer"], talents.MissingCore.Select(t => t.Name));
        Assert.Equal(["Choix exotique"], talents.Atypical.Select(t => t.Name));
        Assert.Equal(0, talents.Similarity);
    }

    /// <summary>
    /// L'API compte des talents hors de l'arbre de la spécialisation (nœuds offerts aux autres spécialisations, rangs d'un
    /// même nœud) : tout le top les « a », personne ne peut les choisir. Ils ne sont jamais manquants.
    /// </summary>
    [Fact]
    public void Talents_outside_the_spec_tree_are_never_missing()
    {
        var top = Top();
        foreach (var p in top)
        {
            p.Talents = [.. p.Talents, new TalentPick(TalentTree.Class, 7, 70, "Explosion de lave (autre spécialisation)", 1)];
        }

        var mine = Player(top[0], talents: [new TalentPick(TalentTree.Spec, 1, 10, "Peau de fer", 1)]);
        var meta = new MetaAggregatorService(Options.Create(new MetaOptions()), PulseRoles.Tank)
            .Aggregate(WowSpec.BloodDeathKnight, "season-mn-2", top, Now) with
            {
                TalentTree = new TalentTreeLayout([new TalentTreeSection(TalentTree.Spec, "Sang", null, 1, 1, 1,
                    [new TalentNodeView(100, 0, 0, TalentNodeKind.Passive, 1, [], [new TalentChoiceView(1, 10, "Peau de fer", null, 1)])])])
            };

        var talents = CharacterAnalyzer.Analyze(PulseRoles.Tank, Blood, meta, top, mine, new Dictionary<int, string>(), Now).Talents;

        Assert.Empty(talents.MissingCore);
        Assert.Equal(1, talents.Similarity);
    }

    [Fact]
    public void Main_stat_below_the_top_range_is_a_problem()
    {
        var top = Top();
        var mine = Player(top[0], stats: Sheet(mastery: 10)); // le top est entre 40 et 50 % de maîtrise

        var analysis = Analyze(top, mine);

        Assert.Equal(Verdict.Problem, analysis.Areas.Single(a => a.Kind == AnalysisAreaKind.Stats).Verdict);
        Assert.Equal(Verdict.Improve, analysis.Stats.Single(s => s.Stat == SecondaryStat.Mastery).Verdict);
        Assert.Contains(analysis.Findings, f => f.Title.StartsWith("Maîtrise trop basse"));
    }

    [Fact]
    public void Missing_embellishments_and_low_item_level_are_flagged()
    {
        var top = Top();
        var gear = top[0].Gear.Select(g => g with { IsEmbellished = false, ItemLevel = g.ItemLevel - 20 }).ToList();

        var analysis = Analyze(top, Player(top[0], gear: gear));

        Assert.Equal(Verdict.Problem, analysis.Areas.Single(a => a.Kind == AnalysisAreaKind.Embellishments).Verdict);
        Assert.Equal(Verdict.Problem, analysis.Areas.Single(a => a.Kind == AnalysisAreaKind.ItemLevel).Verdict);
        Assert.All(analysis.Gear, g => Assert.Equal(Verdict.Improve, g.Verdict));
    }

    [Fact]
    public void Playing_another_spec_is_reported()
    {
        var top = Top();
        var mine = Player(top[0]) with { ActiveSpecId = 251, ActiveSpecName = "Givre" };

        var analysis = Analyze(top, mine);

        Assert.False(analysis.PlaysSpec);
        Assert.Contains(analysis.Findings, f => f.Title.Contains("Givre"));
    }

    [Fact]
    public void A_shared_analysis_reads_back_exactly_as_it_was_stored()
    {
        // Un lien partagé relit la colonne jsonb : tout ce que la page affiche doit survivre à l'aller-retour.
        var top = Top();
        var analysis = Analyze(top, Player(top[0], stats: Sheet(mastery: 30)));

        var json = JsonSerializer.Serialize(analysis, JsonColumn.Options);
        var read = JsonSerializer.Deserialize<CharacterAnalysis>(json, JsonColumn.Options)!;

        Assert.Equal(json, JsonSerializer.Serialize(read, JsonColumn.Options));
        Assert.Equal(analysis.Spec, read.Spec);
        Assert.Equal(analysis.Score, read.Score);
        Assert.Equal(analysis.Findings, read.Findings);
        Assert.Equal(analysis.AnalyzedAtUtc, read.AnalyzedAtUtc);
    }

    [Fact]
    public void Share_tokens_are_random_and_url_safe()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => SharedAnalysisStore.NewToken()).ToList();

        Assert.Equal(100, tokens.Distinct().Count());
        Assert.All(tokens, t => Assert.Matches("^[A-Za-z0-9_-]{22}$", t));
    }

    [Fact]
    public async Task A_share_card_is_a_1200x630_png_even_without_character_images()
    {
        var top = Top();
        var analysis = Analyze(top, Player(top[0], stats: Sheet(mastery: 30)));
        var app = new PulseApp("FrontPulse", PulseRoles.Tank, "M16 3l10 4v8c0 6.5-4.2 11-10 14-5.8-3-10-7.5-10-14V7z");
        var renderer = new ShareCardRenderer(new NoHttp(), app, NullLogger<ShareCardRenderer>.Instance);

        var png = await renderer.RenderAsync(new SharedAnalysis("token", "Hyjal", analysis, Now), CancellationToken.None);

        using var image = SkiaSharp.SKBitmap.Decode(png);
        Assert.Equal((ShareCardRenderer.Width, ShareCardRenderer.Height), (image.Width, image.Height));
        // Pour regarder la vignette : PULSE_SHARE_CARD=chemin.png dotnet test
        if (Environment.GetEnvironmentVariable("PULSE_SHARE_CARD") is { Length: > 0 } output)
        {
            await File.WriteAllBytesAsync(output, png);
        }
    }

    private sealed class NoHttp : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private static CharacterAnalysis Analyze(IReadOnlyList<PlayerProfile> top, CharacterDetails player)
    {
        var meta = new MetaAggregatorService(Options.Create(new MetaOptions()), PulseRoles.Tank)
            .Aggregate(WowSpec.BloodDeathKnight, "season-mn-2", top, Now);
        return CharacterAnalyzer.Analyze(PulseRoles.Tank, Blood, meta, top, player, new Dictionary<int, string>(), Now);
    }

    /// <summary>Dix tanks identiques à quelques indices près : torse enchanté, deux embellissements, 45 % de maîtrise.</summary>
    private static List<PlayerProfile> Top() => Enumerable.Range(1, 10).Select(rank => new PlayerProfile
    {
        Season = "season-mn-2",
        Spec = WowSpec.BloodDeathKnight,
        Rank = rank,
        Name = $"Tank{rank}",
        ItemLevel = 300,
        BestKeyLevel = 20,
        HeroTree = "San'layn",
        LoadoutCode = "TOP",
        Gear =
        [
            new GearPiece(GearSlot.Chest, 1, "Plastron", 300, [], ["Enchanté : Endurance"], [], false),
            new GearPiece(GearSlot.Wrist, 2, "Brassards", 300, [], [], [new ItemEffect(7, "Doublure", null)], true),
            new GearPiece(GearSlot.Back, 3, "Cape", 300, [], [], [new ItemEffect(7, "Doublure", null)], true),
            new GearPiece(GearSlot.Trinket, 4, "Bijou", 300, [], [], [], false)
        ],
        Stats = Sheet(mastery: 40 + rank),
        Talents = [new TalentPick(TalentTree.Spec, 1, 10, "Peau de fer", 1)]
    }).ToList();

    private static CharacterDetails Player(PlayerProfile like, IReadOnlyList<GearPiece>? gear = null, StatSheet? stats = null, IReadOnlyList<TalentPick>? talents = null) =>
        new(
            "Moi",
            "eu",
            "hyjal",
            (int)WowSpec.BloodDeathKnight,
            "Sang",
            new CharacterLoadout((int)WowSpec.BloodDeathKnight, (gear ?? like.Gear).Average(g => g.ItemLevel), gear ?? like.Gear, stats ?? Sheet(mastery: 45), talents ?? like.Talents, "MOI", "San'layn"),
            null,
            null,
            null,
            new MythicPlusProfile(3000, null, 3000, null, null, null, [new MythicPlusRun("Donjon", "DJ", 20, 1, 400, null)], []));

    /// <summary>Indices proportionnels aux pourcentages : la maîtrise domine, puis hâte, critique, polyvalence.</summary>
    private static StatSheet Sheet(double mastery) => new(
        new StatValue(3000, 30), new StatValue(500, 5), new StatValue(mastery * 100, mastery), new StatValue(2500, 25));
}
