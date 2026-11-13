using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Noovio.Pulse.Usage;

namespace PulseAdmin.Stats;

public sealed record FilterOptions(
    IReadOnlyList<(int Id, string Name)> Classes,
    IReadOnlyList<(string App, string Slug, string Name, string ClassName)> Specs,
    IReadOnlyList<string> Regions);

public sealed record BenchPage(IReadOnlyList<BenchEvent> Items, int Total, int Page, int PageSize)
{
    public int Pages => Math.Max(1, (Total + PageSize - 1) / PageSize);
}

/// <summary>Lecture de la base d'usage selon les filtres. Les volumes restent modestes (quelques benchs par minute au plus) : on agrège en mémoire.</summary>
public sealed class UsageQueries(UsageDbContext db, TimeProvider time)
{
    public const int PageSize = 100;

    public async Task<(UsageReport Report, DateTimeOffset FromUtc, DateTimeOffset ToUtc)> GetReportAsync(StatsFilter filter, CancellationToken ct)
    {
        var (from, to) = await RangeAsync(filter, ct);
        var events = await Filtered(filter, from, to).AsNoTracking().ToListAsync(ct);

        var users = events.Select(e => e.BattleNetId).Distinct().ToList();
        var firstSeen = await db.BenchEvents
            .Where(e => users.Contains(e.BattleNetId))
            .GroupBy(e => e.BattleNetId)
            .Select(g => new { g.Key, First = g.Min(e => e.OccurredAtUtc) })
            .ToDictionaryAsync(x => x.Key, x => x.First, ct);

        return (UsageReport.Build(events, firstSeen, from, to, StatsFilter.Zone, uniqueCharacters: !filter.AllBenches), from, to);
    }

    public async Task<BenchPage> GetBenchesAsync(StatsFilter filter, CancellationToken ct)
    {
        var (from, to) = await RangeAsync(filter, ct);
        var query = Filtered(filter, from, to);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(e => e.OccurredAtUtc)
            .Skip((filter.Page - 1) * PageSize)
            .Take(PageSize)
            .AsNoTracking()
            .ToListAsync(ct);
        return new BenchPage(items, total, filter.Page, PageSize);
    }

    public Task<BenchEvent?> GetBenchAsync(long id, CancellationToken ct) =>
        db.BenchEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<FilterOptions> GetOptionsAsync(CancellationToken ct)
    {
        var classes = await db.BenchEvents.Select(e => new { e.ClassId, e.ClassName }).Distinct().ToListAsync(ct);
        var specs = await db.BenchEvents.Where(e => e.SpecSlug != null)
            .Select(e => new { e.App, e.SpecSlug, e.SpecName, e.ClassName }).Distinct().ToListAsync(ct);
        var regions = await db.BenchEvents.Select(e => e.Region).Distinct().ToListAsync(ct);

        return new FilterOptions(
            classes.GroupBy(c => c.ClassId).Select(g => (g.Key, g.First().ClassName)).OrderBy(c => c.Item2).ToList(),
            specs.GroupBy(s => (s.App, s.SpecSlug)).Select(g => (g.Key.App, g.Key.SpecSlug!, g.First().SpecName ?? g.Key.SpecSlug!, g.First().ClassName))
                .OrderBy(s => s.Item4).ThenBy(s => s.Item3).ToList(),
            regions.Order().ToList());
    }

    public async Task<byte[]> ExportCsvAsync(StatsFilter filter, CancellationToken ct)
    {
        var (from, to) = await RangeAsync(filter, ct);
        var events = await Filtered(filter, from, to).OrderBy(e => e.OccurredAtUtc).AsNoTracking().ToListAsync(ct);

        var columns = new (string Name, Func<BenchEvent, object?> Value)[]
        {
            ("id", e => e.Id), ("date_paris", e => TimeZoneInfo.ConvertTime(e.OccurredAtUtc, StatsFilter.Zone).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
            ("app", e => e.App), ("role", e => e.Role), ("statut", e => e.Status), ("cache", e => e.FromCache), ("actualise", e => e.Forced),
            ("duree_ms", e => e.DurationMs), ("compte", e => e.BattleNetId), ("region", e => e.Region), ("royaume", e => e.RealmName),
            ("personnage", e => e.CharacterName), ("classe", e => e.ClassName), ("niveau", e => e.Level), ("race", e => e.Race), ("faction", e => e.Faction),
            ("spe", e => e.SpecName), ("joue_la_spe", e => e.PlaysSpec), ("score", e => e.Score), ("ilvl", e => e.ItemLevel), ("ilvl_top", e => e.TopItemLevel),
            ("hate", e => e.Haste), ("crit", e => e.Crit), ("maitrise", e => e.Mastery), ("polyvalence", e => e.Versatility),
            ("hate_top", e => e.TopHaste), ("crit_top", e => e.TopCrit), ("maitrise_top", e => e.TopMastery), ("polyvalence_top", e => e.TopVersatility),
            ("pieces", e => e.GearPieces), ("pieces_bonnes", e => e.GearGood), ("pieces_a_ameliorer", e => e.GearImprove), ("pieces_probleme", e => e.GearProblem),
            ("popularite_pieces", e => e.GearTopShare), ("enchant_attendus", e => e.EnchantsExpected), ("enchant_manquants", e => e.EnchantsMissing),
            ("chasses", e => e.Sockets), ("chasses_vides", e => e.SocketsEmpty), ("embellissements", e => e.Embellishments),
            ("talents_similarite", e => e.TalentSimilarity), ("heros", e => e.HeroTree), ("heros_top", e => e.TopHeroTree), ("score_mm", e => e.MythicScore),
            ("constats_bons", e => e.FindingsGood), ("constats_a_ameliorer", e => e.FindingsImprove), ("constats_problemes", e => e.FindingsProblem), ("erreur", e => e.Error)
        };

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(';', columns.Select(c => c.Name)));
        foreach (var e in events)
        {
            csv.AppendLine(string.Join(';', columns.Select(c => Cell(c.Value(e)))));
        }

        // BOM : Excel lit alors l'UTF-8 (accents des royaumes et des classes).
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    private static string Cell(object? value) => value switch
    {
        null => "",
        double d => d.ToString("0.####", CultureInfo.GetCultureInfo("fr-FR")),
        bool b => b ? "1" : "0",
        _ => value.ToString() is { } s && (s.Contains(';') || s.Contains('"')) ? $"\"{s.Replace("\"", "\"\"")}\"" : value.ToString() ?? ""
    };

    private async Task<(DateTimeOffset, DateTimeOffset)> RangeAsync(StatsFilter filter, CancellationToken ct)
    {
        DateTimeOffset? first = filter.Period == "tout"
            ? await db.BenchEvents.Select(e => (DateTimeOffset?)e.OccurredAtUtc).MinAsync(ct)
            : null;
        return filter.Range(time.GetUtcNow(), first);
    }

    private IQueryable<BenchEvent> Filtered(StatsFilter filter, DateTimeOffset from, DateTimeOffset to)
    {
        var query = db.BenchEvents.Where(e => e.OccurredAtUtc >= from && e.OccurredAtUtc < to);
        if (filter.App is { } app)
        {
            query = query.Where(e => e.App == app);
        }

        if (filter.ClassId is { } classId)
        {
            query = query.Where(e => e.ClassId == classId);
        }

        if (filter.Spec is { } spec)
        {
            query = query.Where(e => e.SpecSlug == spec);
        }

        if (filter.Region is { } region)
        {
            query = query.Where(e => e.Region == region);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(e => e.Status == status);
        }

        if (filter.User is { } user)
        {
            query = query.Where(e => e.BattleNetId == user);
        }

        if (filter.Search is { } search)
        {
            var pattern = $"%{search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            query = query.Where(e => EF.Functions.ILike(e.CharacterName, pattern) || EF.Functions.ILike(e.RealmName, pattern));
        }

        return query;
    }
}
