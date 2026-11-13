using Noovio.Pulse.Application.Analysis;
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
        var bench = new BenchEvent
        {
            OccurredAtUtc = nowUtc,
            App = app.Name,
            Role = app.Role.RaiderIoRole,
            Status = error is not null ? BenchStatus.Error : result?.Status.ToString() ?? BenchStatus.Error,
            Error = error,
            FromCache = fromCache,
            Forced = forced,
            DurationMs = (int)Math.Min(int.MaxValue, duration.TotalMilliseconds),
            BattleNetId = battleNetId,
            Region = character.Region,
            RealmSlug = character.RealmSlug,
            RealmName = character.RealmName,
            CharacterName = character.Name,
            ClassId = character.ClassId,
            ClassName = character.ClassName,
            Level = character.Level,
            Race = character.RaceName,
            Faction = character.Faction
        };

        if (result?.Spec is { } spec)
        {
            bench.SpecId = (int)spec.Spec;
            bench.SpecSlug = spec.Slug;
            bench.SpecName = spec.SpecName;
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
