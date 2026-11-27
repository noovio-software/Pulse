namespace PulseAdmin.Notifications;

/// <summary>
/// Les heures de récap (heure de Paris). Chaque récap couvre la période depuis le précédent :
/// à 7h, de 21h la veille à 7h ; à 12h, de 7h à 12h ; et ainsi de suite.
/// </summary>
public sealed class DigestSchedule
{
    private readonly TimeOnly[] _times;
    private readonly TimeZoneInfo _zone;

    public DigestSchedule(IEnumerable<TimeOnly> times, TimeZoneInfo zone)
    {
        _times = times.Distinct().Order().ToArray();
        if (_times.Length == 0)
        {
            throw new ArgumentException("Au moins une heure de récap est nécessaire.", nameof(times));
        }
        _zone = zone;
    }

    public IReadOnlyList<TimeOnly> Times => _times;

    /// <summary>La dernière période terminée à <paramref name="nowUtc"/> : son début (récap précédent) et sa fin (dernier récap).</summary>
    public (DateTimeOffset StartUtc, DateTimeOffset EndUtc) LatestPeriod(DateTimeOffset nowUtc)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, _zone).Date);

        // Trois jours suffisent : la veille porte toujours au moins un récap, et l'avant-veille le précédent.
        var slots = Enumerable.Range(-2, 3)
            .SelectMany(offset => _times.Select(time => ToUtc(today.AddDays(offset), time)))
            .Where(slot => slot <= nowUtc)
            .Order()
            .ToList();

        return (slots[^2], slots[^1]);
    }

    private DateTimeOffset ToUtc(DateOnly day, TimeOnly time)
    {
        var local = day.ToDateTime(time);
        return new DateTimeOffset(local, _zone.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>« 21h → 7h », en heure de Paris.</summary>
    public string Label(DateTimeOffset startUtc, DateTimeOffset endUtc) => $"{Hour(startUtc)} → {Hour(endUtc)}";

    private string Hour(DateTimeOffset utc)
    {
        var local = TimeZoneInfo.ConvertTime(utc, _zone);
        return local.Minute == 0 ? $"{local.Hour}h" : $"{local.Hour}h{local.Minute:00}";
    }
}
