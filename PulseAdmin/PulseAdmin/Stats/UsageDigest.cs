namespace PulseAdmin.Stats;

/// <summary>Les colonnes d'un bench dont le résumé a besoin : la requête ne lit pas le reste de la ligne.</summary>
public sealed record DigestRow(DateTimeOffset OccurredAtUtc, string App, long? BattleNetId, string Region, string RealmName, string CharacterName);

/// <summary>
/// Un utilisateur de la période, une seule fois : par compte Battle.net, ou par personnage pour un import de l'addon
/// fait sans être connecté. Le nom est celui du dernier personnage benché.
/// </summary>
public sealed record DigestPerson(
    string Name,
    string Realm,
    long? BattleNetId,
    int Benches,
    IReadOnlyList<string> Apps,
    IReadOnlyList<string> Characters,
    DateTimeOffset LastSeenUtc);

public sealed record DigestApp(string App, int Benches);

/// <summary>
/// Le résumé d'une période, sans filtre : le bandeau « Aujourd'hui » du tableau de bord, les notifications
/// et le bot Discord lisent les mêmes chiffres.
/// </summary>
public sealed record UsageDigest(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int Benches,
    int Characters,
    IReadOnlyList<DigestApp> Apps,
    IReadOnlyList<DigestPerson> People,
    DateTimeOffset? LastBenchUtc)
{
    public int Users => People.Count;

    public static UsageDigest Build(IEnumerable<DigestRow> rows, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        var events = rows.OrderBy(r => r.OccurredAtUtc).ToList();

        var people = events
            .GroupBy(r => r.BattleNetId is { } id ? $"bnet:{id}" : $"perso:{r.Region}/{r.RealmName}/{r.CharacterName}".ToLowerInvariant())
            .Select(g =>
            {
                var last = g.Last();
                return new DigestPerson(
                    last.CharacterName,
                    last.RealmName,
                    last.BattleNetId,
                    g.Count(),
                    g.Select(r => r.App).Distinct().Order(StringComparer.Ordinal).ToList(),
                    g.Select(r => r.CharacterName).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    last.OccurredAtUtc);
            })
            .OrderByDescending(p => p.Benches)
            .ThenByDescending(p => p.LastSeenUtc)
            .ToList();

        return new UsageDigest(
            fromUtc,
            toUtc,
            events.Count,
            events.Select(r => (r.Region, r.RealmName, r.CharacterName.ToLowerInvariant())).Distinct().Count(),
            events.GroupBy(r => r.App).Select(g => new DigestApp(g.Key, g.Count())).OrderByDescending(a => a.Benches).ToList(),
            people,
            events.Count == 0 ? null : events[^1].OccurredAtUtc);
    }

    /// <summary>« Alice, Bob, Carl +2 » : les noms les plus actifs d'abord, coupés pour tenir dans une notification.</summary>
    public string Names(int max)
    {
        var shown = People.Take(max).Select(p => p.Name);
        var rest = People.Count - max;
        return string.Join(", ", shown) + (rest > 0 ? $" +{rest}" : "");
    }
}
