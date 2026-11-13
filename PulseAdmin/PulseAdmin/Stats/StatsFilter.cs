using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;

namespace PulseAdmin.Stats;

/// <summary>
/// Les filtres, portés par l'adresse (formulaire GET) : une vue se partage et se met en favori.
/// Les jours sont ceux de Paris ; la période va de <see cref="FromUtc"/> inclus à <see cref="ToUtc"/> exclu.
/// </summary>
public sealed record StatsFilter(
    string Period,
    DateOnly? From,
    DateOnly? To,
    string? App,
    int? ClassId,
    string? Spec,
    string? Region,
    string? Status,
    long? User,
    string? Search,
    bool AllBenches,
    int Page)
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

    public static readonly IReadOnlyList<(string Key, string Label)> Periods =
    [
        ("24h", "24 heures"), ("7j", "7 jours"), ("30j", "30 jours"), ("90j", "90 jours"), ("365j", "1 an"), ("tout", "Tout"), ("dates", "Dates…")
    ];

    public static StatsFilter Parse(IQueryCollection query)
    {
        string? Text(string key) => query[key].ToString() is { Length: > 0 } v ? v.Trim() : null;
        DateOnly? Date(string key) => DateOnly.TryParseExact(Text(key), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

        var period = Text("periode") is { } p && Periods.Any(x => x.Key == p) ? p : "30j";
        return new StatsFilter(
            period,
            Date("du"),
            Date("au"),
            Text("app"),
            int.TryParse(Text("classe"), out var c) ? c : null,
            Text("spe"),
            Text("region"),
            Text("statut"),
            long.TryParse(Text("compte"), out var u) ? u : null,
            Text("q"),
            Text("tous") == "1",
            int.TryParse(Text("page"), out var page) && page > 1 ? page : 1);
    }

    /// <summary>Bornes UTC de la période. Pour « tout », <paramref name="firstEventUtc"/> sert de début.</summary>
    public (DateTimeOffset FromUtc, DateTimeOffset ToUtc) Range(DateTimeOffset nowUtc, DateTimeOffset? firstEventUtc)
    {
        DateTimeOffset StartOfDay(DateOnly day) =>
            new(day.ToDateTime(TimeOnly.MinValue), Zone.GetUtcOffset(day.ToDateTime(TimeOnly.MinValue)));

        return Period switch
        {
            "24h" => (nowUtc.AddHours(-24), nowUtc),
            "7j" => (nowUtc.AddDays(-7), nowUtc),
            "90j" => (nowUtc.AddDays(-90), nowUtc),
            "365j" => (nowUtc.AddDays(-365), nowUtc),
            "tout" => (firstEventUtc is { } first && first < nowUtc ? first : nowUtc.AddDays(-1), nowUtc),
            "dates" => (
                StartOfDay(From ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, Zone).Date).AddDays(-30)).ToUniversalTime(),
                To is { } to ? StartOfDay(to.AddDays(1)).ToUniversalTime() : nowUtc),
            _ => (nowUtc.AddDays(-30), nowUtc)
        };
    }

    /// <summary>L'adresse de la même vue avec quelques valeurs changées (null : retirée).</summary>
    public string Url(string path, params (string Key, string? Value)[] changes)
    {
        var values = new Dictionary<string, string?>
        {
            ["periode"] = Period == "30j" ? null : Period,
            ["du"] = Period == "dates" ? From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null,
            ["au"] = Period == "dates" ? To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null,
            ["app"] = App,
            ["classe"] = ClassId?.ToString(CultureInfo.InvariantCulture),
            ["spe"] = Spec,
            ["region"] = Region,
            ["statut"] = Status,
            ["compte"] = User?.ToString(CultureInfo.InvariantCulture),
            ["q"] = Search,
            ["tous"] = AllBenches ? "1" : null
        };
        foreach (var (key, value) in changes)
        {
            values[key] = value;
        }

        return QueryHelpers.AddQueryString(path, values.Where(v => v.Value is { Length: > 0 }).Select(v => KeyValuePair.Create(v.Key, v.Value)));
    }
}
