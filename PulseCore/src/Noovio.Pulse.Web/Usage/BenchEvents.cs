using Noovio.Pulse.Application.Analysis;
using Noovio.Pulse.Application.Import;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Enums;
using Noovio.Pulse.Domain.Roles;
using Noovio.Pulse.Usage;

namespace Noovio.Pulse.Web.Usage;

/// <summary>Traduit un bench (le personnage, et l'analyse quand elle a abouti) en ligne de la base d'usage.</summary>
public static class BenchEvents
{
    public static BenchEvent Create(
        PulseApp app,
        long battleNetId,
        UserCharacter character,
        AnalysisResult? result,
        string? error,
        bool fromCache,
        bool forced,
        TimeSpan duration,
        DateTimeOffset nowUtc)
    {
        var bench = Describe(Start(app, result, error, fromCache, forced, duration, nowUtc), character);
        bench.Source = BenchSource.BattleNet;
        bench.BattleNetId = battleNetId;
        return Finish(bench, result);
    }

    /// <summary>Bench d'un personnage trouvé par recherche : le compte seulement si le visiteur est connecté.</summary>
    public static BenchEvent CreateSearch(
        PulseApp app,
        long? battleNetId,
        UserCharacter character,
        AnalysisResult? result,
        string? error,
        bool fromCache,
        bool forced,
        TimeSpan duration,
        DateTimeOffset nowUtc)
    {
        var bench = Describe(Start(app, result, error, fromCache, forced, duration, nowUtc), character);
        bench.Source = BenchSource.Search;
        bench.BattleNetId = battleNetId;
        return Finish(bench, result);
    }

    private static BenchEvent Describe(BenchEvent bench, UserCharacter character)
    {
        bench.Region = character.Region;
        bench.RealmSlug = character.RealmSlug;
        bench.RealmName = character.RealmName;
        bench.CharacterName = character.Name;
        bench.ClassId = character.ClassId;
        bench.ClassName = ClassName(character.ClassId, character.ClassName);
        bench.Level = character.Level;
        bench.Race = character.RaceName;
        bench.Faction = character.Faction;
        return bench;
    }

    /// <summary>Bench d'une chaîne collée depuis l'addon : le personnage vient de l'export, le compte seulement si le visiteur est connecté.</summary>
    public static BenchEvent CreateImport(
        PulseApp app,
        long? battleNetId,
        CharacterImport import,
        AnalysisResult? result,
        string? error,
        bool fromCache,
        bool forced,
        TimeSpan duration,
        DateTimeOffset nowUtc)
    {
        var who = import.Character;
        var bench = Start(app, result, error, fromCache, forced, duration, nowUtc);
        bench.Source = BenchSource.Addon;
        bench.BattleNetId = battleNetId;
        bench.Region = Cut(who?.Region?.ToLowerInvariant(), 4);
        bench.RealmSlug = Cut(who?.RealmNormalized ?? who?.Realm, 64);
        bench.RealmName = Cut(who?.Realm ?? who?.RealmNormalized, 64);
        bench.CharacterName = Cut(who?.Name, 64);
        bench.ClassId = who?.Class ?? 0;
        bench.ClassName = Cut(who is null ? null : ClassName(who.Class, null), 64);
        bench.Level = who?.Level ?? 0;
        bench.Race = who?.Race is { } race ? Cut(race, 64) : null;
        bench.Faction = who?.Faction is { } faction ? Cut(faction, 16) : null;
        return Finish(bench, result);
    }

    private static BenchEvent Start(PulseApp app, AnalysisResult? result, string? error, bool fromCache, bool forced, TimeSpan duration, DateTimeOffset nowUtc) => new()
    {
        OccurredAtUtc = nowUtc,
        App = app.Name,
        Role = app.Role.RaiderIoRole,
        Status = error is not null ? BenchStatus.Error : result?.Status.ToString() ?? BenchStatus.Error,
        Error = error,
        FromCache = fromCache,
        Forced = forced,
        DurationMs = (int)Math.Min(int.MaxValue, duration.TotalMilliseconds)
    };

    /// <summary>
    /// Toujours en français : la base d'usage est lue par PulseAdmin, qui regroupe par classe. Le nom renvoyé par Blizzard
    /// ou l'interface suit la langue du visiteur (« Shaman » à côté de « Chaman »). Repli sur ce nom pour une classe inconnue.
    /// </summary>
    private static string ClassName(int classId, string? fallback) =>
        WowClasses.FrenchName(classId) is { Length: > 0 } name ? name : fallback ?? "";

    /// <summary>Les textes d'un export viennent du jeu : coupés à la taille des colonnes plutôt que de faire échouer l'écriture.</summary>
    private static string Cut(string? text, int max) => text is null ? "" : text.Length <= max ? text : text[..max];

    private static BenchEvent Finish(BenchEvent bench, AnalysisResult? result)
    {
        if (result?.Spec is { } spec)
        {
            bench.SpecId = (int)spec.Spec;
            bench.SpecSlug = spec.Slug;
            bench.SpecName = spec.FrenchSpecName;
        }

        if (result?.Analysis is { } analysis)
        {
            Fill(bench, analysis);
        }

        return bench;
    }

    private static void Fill(BenchEvent bench, CharacterAnalysis analysis)
    {
        var loadout = analysis.Character.Loadout;

        bench.PlaysSpec = analysis.PlaysSpec;
        bench.Score = analysis.Score;
        bench.ItemLevel = loadout.ItemLevel;
        bench.TopItemLevel = analysis.Meta.MedianItemLevel;

        foreach (var stat in analysis.Stats)
        {
            var top = stat.Top.MedianPercent;
            switch (stat.Stat)
            {
                case SecondaryStat.Haste:
                    (bench.Haste, bench.HasteRating, bench.TopHaste) = (stat.Percent, stat.Rating, top);
                    break;
                case SecondaryStat.Crit:
                    (bench.Crit, bench.CritRating, bench.TopCrit) = (stat.Percent, stat.Rating, top);
                    break;
                case SecondaryStat.Mastery:
                    (bench.Mastery, bench.MasteryRating, bench.TopMastery) = (stat.Percent, stat.Rating, top);
                    break;
                case SecondaryStat.Versatility:
                    (bench.Versatility, bench.VersatilityRating, bench.TopVersatility) = (stat.Percent, stat.Rating, top);
                    break;
            }
        }

        var gear = analysis.Gear;
        bench.GearPieces = gear.Count;
        bench.GearGood = gear.Count(g => g.Verdict == Verdict.Good);
        bench.GearImprove = gear.Count(g => g.Verdict == Verdict.Improve);
        bench.GearProblem = gear.Count(g => g.Verdict == Verdict.Problem);
        bench.GearTopShare = gear.Count == 0 ? null : gear.Average(g => g.TopShare);
        bench.EnchantsExpected = gear.Count(g => g.EnchantExpected);
        bench.EnchantsMissing = gear.Count(g => g.EnchantExpected && !g.HasEnchant);

        var sockets = loadout.Gear.SelectMany(g => g.Sockets ?? []).ToList();
        bench.Sockets = sockets.Count;
        bench.SocketsEmpty = sockets.Count(s => s.GemId is null);
        bench.Embellishments = loadout.Gear.Count(g => g.IsEmbellished);

        bench.TalentSimilarity = analysis.Talents.Similarity;
        bench.HeroTree = analysis.Talents.HeroTree;
        bench.TopHeroTree = analysis.Talents.TopHeroTree;
        bench.MythicScore = analysis.Character.MythicPlus?.RoleScore is > 0 and var role ? role : analysis.Character.MythicPlus?.Score;

        bench.FindingsGood = analysis.Findings.Count(f => f.Verdict == Verdict.Good);
        bench.FindingsImprove = analysis.Findings.Count(f => f.Verdict == Verdict.Improve);
        bench.FindingsProblem = analysis.Findings.Count(f => f.Verdict == Verdict.Problem);
        bench.Areas = analysis.Areas.ToDictionary(a => a.Kind.ToString(), a => a.Verdict.ToString());

        // Anneaux et bijoux partagent un emplacement agrégé : la moyenne des deux pièces.
        bench.SlotItemLevels = loadout.Gear
            .GroupBy(g => g.Slot)
            .ToDictionary(g => g.Key.ToString(), g => (int)Math.Round(g.Average(p => p.ItemLevel)));
    }
}
