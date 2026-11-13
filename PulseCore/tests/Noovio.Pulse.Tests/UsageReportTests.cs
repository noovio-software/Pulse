using Noovio.Pulse.Usage;

namespace Noovio.Pulse.Tests;

public class UsageReportTests
{
    private static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
    private static readonly DateTimeOffset From = new(2026, 11, 10, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 11, 13, 0, 0, 0, TimeSpan.Zero);

    private static BenchEvent Bench(long user, string name, DateTimeOffset at, double? ilvl = 680, string status = BenchStatus.Ok, string app = "BurstPulse") => new()
    {
        BattleNetId = user,
        App = app,
        Role = "dps",
        Status = status,
        OccurredAtUtc = at,
        Region = "eu",
        RealmSlug = "hyjal",
        RealmName = "Hyjal",
        CharacterName = name,
        ClassId = 8,
        ClassName = "Mage",
        SpecSlug = "fire",
        SpecName = "Feu",
        ItemLevel = status == BenchStatus.Ok ? ilvl : null,
        TopItemLevel = status == BenchStatus.Ok ? 690 : null,
        Haste = status == BenchStatus.Ok ? 20 : null,
        TopHaste = status == BenchStatus.Ok ? 25 : null,
        EnchantsExpected = status == BenchStatus.Ok ? 8 : null,
        EnchantsMissing = status == BenchStatus.Ok ? 2 : null,
        Areas = status == BenchStatus.Ok ? new() { ["ItemLevel"] = "Problem", ["Stats"] = "Good" } : null
    };

    [Fact]
    public void Counts_users_characters_and_outcomes()
    {
        var events = new[]
        {
            Bench(1, "Alpha", From.AddHours(10)),
            Bench(1, "Alpha", From.AddHours(11)),
            Bench(1, "Beta", From.AddDays(1)),
            Bench(2, "Gamma", From.AddHours(12), status: BenchStatus.CharacterNotFound)
        };
        var firstSeen = new Dictionary<long, DateTimeOffset> { [1] = From.AddDays(-20), [2] = From.AddHours(12) };

        var report = UsageReport.Build(events, firstSeen, From, To, Paris);

        Assert.Equal(4, report.Kpis.Benches);
        Assert.Equal(2, report.Kpis.Users);
        Assert.Equal(3, report.Kpis.Characters);
        Assert.Equal(1, report.Kpis.NewUsers);
        Assert.Equal(1, report.Kpis.ReturningUsers);
        Assert.Equal(0.75, report.Kpis.SuccessRate);
    }

    [Fact]
    public void Profile_keeps_the_last_bench_of_each_character_unless_asked_otherwise()
    {
        var events = new[]
        {
            Bench(1, "Alpha", From.AddHours(1), ilvl: 670),
            Bench(1, "Alpha", From.AddHours(2), ilvl: 680),
            Bench(2, "Beta", From.AddHours(3), ilvl: 690)
        };

        var unique = UsageReport.Build(events, new Dictionary<long, DateTimeOffset>(), From, To, Paris);
        var all = UsageReport.Build(events, new Dictionary<long, DateTimeOffset>(), From, To, Paris, uniqueCharacters: false);

        Assert.Equal(2, unique.ProfileSample);
        Assert.Equal(685, unique.ItemLevel.Average);
        Assert.Equal(-5, unique.ItemLevel.AverageGap);
        Assert.Equal(680, all.ItemLevel.Average);
    }

    [Fact]
    public void Aggregates_stats_gear_and_verdicts()
    {
        var events = new[] { Bench(1, "Alpha", From.AddHours(1)), Bench(2, "Beta", From.AddHours(2)) };

        var report = UsageReport.Build(events, new Dictionary<long, DateTimeOffset>(), From, To, Paris);

        var haste = report.Stats.Single(s => s.Name == "Hâte");
        Assert.Equal(20, haste.AveragePercent);
        Assert.Equal(-5, haste.AverageGap);
        Assert.Equal(0.75, report.Gear.EnchantCoverage);
        Assert.Equal(0, report.Gear.FullyEnchantedRate);
        var itemLevel = report.Areas.Single(a => a.Area == "ItemLevel");
        Assert.Equal(2, itemLevel.Problem);
        Assert.Equal(0, itemLevel.Good);
    }

    [Fact]
    public void Hours_and_days_are_in_paris_time_and_days_have_no_gaps()
    {
        // 23:30 UTC le 10 novembre : 0h30 le 11 à Paris (UTC+1 en hiver).
        var events = new[] { Bench(1, "Alpha", From.AddHours(23.5)) };

        var report = UsageReport.Build(events, new Dictionary<long, DateTimeOffset>(), From, To, Paris);

        Assert.Equal(1, report.HourlyByApp["BurstPulse"][0]);
        Assert.Equal(new DateOnly(2026, 11, 10), report.Daily[0].Day);
        Assert.Equal(new DateOnly(2026, 11, 13), report.Daily[^1].Day);
        Assert.Equal(1, report.Daily.Single(d => d.Day == new DateOnly(2026, 11, 11)).Total);
        // Mercredi 11 novembre 2026 : troisième ligne, lundi en premier.
        Assert.Equal(1, report.WeekHour[2, 0]);
    }

    [Fact]
    public void Histogram_bins_include_empty_ones_between_values()
    {
        var bins = UsageReport.Histogram([670, 671, 679], 3);

        Assert.Equal([669, 672, 675, 678], bins.Select(b => b.Low));
        Assert.Equal([2, 0, 0, 1], bins.Select(b => b.Count));
    }

    [Fact]
    public void Percentile_interpolates()
    {
        Assert.Equal(2.5, UsageReport.Percentile([1, 2, 3, 4], 0.5));
        Assert.Null(UsageReport.Percentile([], 0.5));
    }
}
