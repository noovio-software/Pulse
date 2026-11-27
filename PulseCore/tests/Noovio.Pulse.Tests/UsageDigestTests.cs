using PulseAdmin.Notifications;
using PulseAdmin.Stats;

namespace Noovio.Pulse.Tests;

public class UsageDigestTests
{
    private static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
    private static readonly DigestSchedule Schedule = new([new(7, 0), new(12, 0), new(18, 0), new(21, 0)], Paris);

    /// <summary>Une heure de Paris, en UTC.</summary>
    private static DateTimeOffset At(int year, int month, int day, int hour, int minute = 0)
    {
        var local = new DateTime(year, month, day, hour, minute, 0);
        return new DateTimeOffset(local, Paris.GetUtcOffset(local)).ToUniversalTime();
    }

    [Theory]
    [InlineData(7, 0, 26, 21, 27, 7)]   // le récap de 7h couvre la nuit, depuis 21h la veille
    [InlineData(9, 30, 26, 21, 27, 7)]  // entre deux récaps : la dernière période terminée
    [InlineData(12, 0, 27, 7, 27, 12)]
    [InlineData(18, 5, 27, 12, 27, 18)]
    [InlineData(21, 0, 27, 18, 27, 21)]
    [InlineData(23, 59, 27, 18, 27, 21)]
    [InlineData(3, 0, 26, 18, 26, 21)]  // avant 7h : la période de la veille au soir
    public void Each_digest_covers_the_time_since_the_previous_one(int hour, int minute, int startDay, int startHour, int endDay, int endHour)
    {
        var (start, end) = Schedule.LatestPeriod(At(2026, 11, 27, hour, minute));

        Assert.Equal(At(2026, 11, startDay, startHour), start);
        Assert.Equal(At(2026, 11, endDay, endHour), end);
    }

    [Fact]
    public void Periods_follow_paris_time_across_the_summer_time_change()
    {
        // Passage à l'heure d'été dans la nuit du 28 au 29 mars 2026 : 7h vaut 6h UTC la veille, 5h UTC le lendemain.
        var (start, end) = Schedule.LatestPeriod(At(2026, 3, 29, 7, 10));

        Assert.Equal(new DateTimeOffset(2026, 3, 28, 20, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 5, 0, 0, TimeSpan.Zero), end);
        Assert.Equal("21h → 7h", Schedule.Label(start, end));
    }

    [Fact]
    public void Users_are_counted_once_by_account_and_named_after_their_latest_character()
    {
        var t = At(2026, 11, 27, 8);
        var rows = new[]
        {
            new DigestRow(t, "FrontPulse", 1, "eu", "Hyjal", "Tankette"),
            new DigestRow(t.AddMinutes(5), "BurstPulse", 1, "eu", "Hyjal", "Pyromane"),
            new DigestRow(t.AddMinutes(6), "BurstPulse", 1, "eu", "Hyjal", "Pyromane"),
            new DigestRow(t.AddMinutes(10), "LifePulse", 2, "eu", "Elune", "Soigneuse"),
            // Import de l'addon sans être connecté : pas de compte, le personnage tient lieu d'utilisateur.
            new DigestRow(t.AddMinutes(12), "LifePulse", null, "eu", "Elune", "Anonyme"),
            new DigestRow(t.AddMinutes(13), "LifePulse", null, "eu", "Elune", "anonyme"),
        };

        var digest = UsageDigest.Build(rows, t, t.AddHours(1));

        Assert.Equal(6, digest.Benches);
        Assert.Equal(3, digest.Users);
        Assert.Equal(4, digest.Characters);
        Assert.Equal(t.AddMinutes(13), digest.LastBenchUtc);

        var first = digest.People[0];
        Assert.Equal("Pyromane", first.Name);
        Assert.Equal(3, first.Benches);
        Assert.Equal(["BurstPulse", "FrontPulse"], first.Apps);
        Assert.Equal(["Tankette", "Pyromane"], first.Characters);
        Assert.Null(digest.People.Single(p => p.Benches == 2).BattleNetId);

        Assert.Equal("Pyromane, anonyme +1", digest.Names(2));
        Assert.Equal(3, digest.Apps.Single(a => a.App == "LifePulse").Benches);
    }

    [Fact]
    public void The_notification_reads_at_a_glance()
    {
        var t = At(2026, 11, 27, 8);
        var digest = UsageDigest.Build([new DigestRow(t, "FrontPulse", 1, "eu", "Hyjal", "Tankette")], t, t.AddHours(4));

        var message = DigestText.Build(digest, "7h → 12h");

        Assert.Equal("PulseMetrics · 7h → 12h", message.Title);
        Assert.Equal("1 bench · 1 utilisateur\nTankette", message.Body);
        Assert.Equal("Aucun bench sur la période.", DigestText.Build(UsageDigest.Build([], t, t.AddHours(4)), "7h → 12h").Body);
    }
}
