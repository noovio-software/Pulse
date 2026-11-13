namespace Noovio.Pulse.Usage;

/// <summary>
/// Tout ce que PulseAdmin affiche, calculé en mémoire à partir des benchs de la période filtrée.
/// Deux échantillons : l'usage (tous les benchs, toutes issues) et le profil des personnages
/// (benchs aboutis ; par défaut le dernier de chaque personnage et spécialisation, pour qu'un joueur
/// qui actualise dix fois ne pèse pas dix fois dans les moyennes).
/// </summary>
public sealed record UsageReport(
    UsageKpis Kpis,
    IReadOnlyList<DailyUsage> Daily,
    IReadOnlyDictionary<string, int[]> HourlyByApp,
    int[,] WeekHour,
    IReadOnlyList<AppUsage> Apps,
    IReadOnlyList<Share> Statuses,
    int ProfileSample,
    ItemLevelSummary ItemLevel,
    IReadOnlyList<StatSummary> Stats,
    GearSummary Gear,
    IReadOnlyList<AreaVerdicts> Areas,
    IReadOnlyList<SlotItemLevel> Slots,
    IReadOnlyList<ClassUsage> Classes,
    IReadOnlyList<SpecUsage> Specs,
    IReadOnlyList<Share> Regions,
    IReadOnlyList<Share> Realms,
    IReadOnlyList<Share> Factions,
    IReadOnlyList<Share> Races,
    IReadOnlyList<Share> HeroTrees,
    IReadOnlyList<UserUsage> TopUsers,
    IReadOnlyList<BenchEvent> Recent)
{
    public static readonly string[] AppNames = ["FrontPulse", "LifePulse", "BurstPulse"];

    /// <summary>Ordre d'affichage des domaines, celui de la page d'analyse.</summary>
    public static readonly string[] AreaNames = ["ItemLevel", "Stats", "Talents", "Trinkets", "Embellishments", "Enchantments", "Gems", "MythicPlus"];

    public static readonly string[] SlotNames =
    [
        "Head", "Neck", "Shoulder", "Back", "Chest", "Wrist", "Hands", "Waist", "Legs", "Feet",
        "Finger", "Trinket", "MainHand", "OffHand"
    ];

    /// <param name="events">Benchs de la période, filtres appliqués.</param>
    /// <param name="firstSeen">Premier bench de chaque compte, toutes périodes confondues : distingue les nouveaux utilisateurs.</param>
    /// <param name="zone">Fuseau des courbes par heure et par jour (Europe/Paris).</param>
    /// <param name="uniqueCharacters">Profil : le dernier bench abouti de chaque personnage, sinon tous.</param>
    public static UsageReport Build(
        IReadOnlyList<BenchEvent> events,
        IReadOnlyDictionary<long, DateTimeOffset> firstSeen,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        TimeZoneInfo zone,
        bool uniqueCharacters = true)
    {
        DateTimeOffset Local(DateTimeOffset t) => TimeZoneInfo.ConvertTime(t, zone);

        var ok = events.Where(e => e.Status == BenchStatus.Ok).ToList();
        var profile = uniqueCharacters
            ? ok.GroupBy(e => (e.App, e.Region, Realm: e.RealmSlug.ToLowerInvariant(), Name: e.CharacterName.ToLowerInvariant(), e.SpecSlug))
                .Select(g => g.MaxBy(e => e.OccurredAtUtc)!)
                .ToList()
            : ok;

        var users = events.Select(e => e.BattleNetId).Distinct().ToList();
        var computed = events.Where(e => !e.FromCache && e.Status != BenchStatus.Error).Select(e => (double)e.DurationMs).ToList();
        var kpis = new UsageKpis(
            Benches: events.Count,
            Users: users.Count,
            Characters: events.Select(CharacterKey).Distinct().Count(),
            NewUsers: users.Count(u => firstSeen.TryGetValue(u, out var first) && first >= fromUtc && first < toUtc),
            ReturningUsers: events.GroupBy(e => e.BattleNetId).Count(g => g.Select(e => Local(e.OccurredAtUtc).Date).Distinct().Count() > 1),
            SuccessRate: Ratio(ok.Count, events.Count),
            CacheRate: Ratio(events.Count(e => e.FromCache), events.Count),
            ForcedRate: Ratio(events.Count(e => e.Forced), events.Count),
            AverageDurationMs: Average(computed),
            P95DurationMs: Percentile(computed, 0.95),
            BenchesPerUser: users.Count == 0 ? null : (double)events.Count / users.Count);

        // Jours de la période, vides compris : une courbe sans trous.
        var firstDay = DateOnly.FromDateTime(Local(fromUtc).Date);
        var lastDay = DateOnly.FromDateTime(Local(toUtc.AddTicks(-1)).Date);
        var byDay = events.GroupBy(e => DateOnly.FromDateTime(Local(e.OccurredAtUtc).Date)).ToDictionary(g => g.Key, g => g.ToList());
        var daily = new List<DailyUsage>();
        for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
        {
            var list = byDay.GetValueOrDefault(day) ?? [];
            daily.Add(new DailyUsage(day, AppNames.ToDictionary(a => a, a => list.Count(e => e.App == a)), list.Select(e => e.BattleNetId).Distinct().Count()));
        }

        var hourly = AppNames.ToDictionary(a => a, _ => new int[24]);
        var weekHour = new int[7, 24];
        foreach (var e in events)
        {
            var local = Local(e.OccurredAtUtc);
            if (hourly.TryGetValue(e.App, out var hours))
            {
                hours[local.Hour]++;
            }

            // Lundi en premier.
            weekHour[((int)local.DayOfWeek + 6) % 7, local.Hour]++;
        }

        var apps = AppNames
            .Select(a => new AppUsage(a, events.Count(e => e.App == a), events.Where(e => e.App == a).Select(e => e.BattleNetId).Distinct().Count(),
                events.Where(e => e.App == a).Select(CharacterKey).Distinct().Count(), Average(profile.Where(e => e.App == a).Select(e => e.ItemLevel))))
            .ToList();

        var ilvls = profile.Select(e => e.ItemLevel).OfType<double>().ToList();
        var itemLevel = new ItemLevelSummary(
            Average(ilvls), Percentile(ilvls, 0.5), ilvls.Count == 0 ? null : ilvls.Min(), ilvls.Count == 0 ? null : ilvls.Max(),
            Percentile(ilvls, 0.25), Percentile(ilvls, 0.75),
            Average(profile.Where(e => e.ItemLevel is not null && e.TopItemLevel is not null).Select(e => e.ItemLevel - e.TopItemLevel)),
            Histogram(ilvls, 3),
            ilvls.Count);

        var stats = new List<StatSummary>
        {
            Stat("Hâte", profile, e => e.Haste, e => e.TopHaste, e => e.HasteRating),
            Stat("Critique", profile, e => e.Crit, e => e.TopCrit, e => e.CritRating),
            Stat("Maîtrise", profile, e => e.Mastery, e => e.TopMastery, e => e.MasteryRating),
            Stat("Polyvalence", profile, e => e.Versatility, e => e.TopVersatility, e => e.VersatilityRating)
        };

        var withGear = profile.Where(e => e.GearPieces is > 0).ToList();
        var gear = new GearSummary(
            GoodPieceRate: Average(withGear.Select(e => (double?)e.GearGood / e.GearPieces)),
            ProblemPieceRate: Average(withGear.Select(e => (double?)e.GearProblem / e.GearPieces)),
            TopShare: Average(profile.Select(e => e.GearTopShare)),
            EnchantCoverage: Ratio(
                profile.Sum(e => (e.EnchantsExpected ?? 0) - (e.EnchantsMissing ?? 0)),
                profile.Sum(e => e.EnchantsExpected ?? 0)),
            FullyEnchantedRate: Ratio(profile.Count(e => e.EnchantsExpected is not null && e.EnchantsMissing == 0), profile.Count(e => e.EnchantsExpected is not null)),
            SocketFill: Ratio(profile.Sum(e => (e.Sockets ?? 0) - (e.SocketsEmpty ?? 0)), profile.Sum(e => e.Sockets ?? 0)),
            AverageSockets: Average(profile.Select(e => (double?)e.Sockets)),
            AverageEmbellishments: Average(profile.Select(e => (double?)e.Embellishments)),
            TalentSimilarity: Average(profile.Select(e => e.TalentSimilarity)),
            TopHeroTreeRate: Ratio(profile.Count(e => e.HeroTree is not null && e.HeroTree == e.TopHeroTree), profile.Count(e => e.TopHeroTree is not null)),
            PlaysSpecRate: Ratio(profile.Count(e => e.PlaysSpec == true), profile.Count(e => e.PlaysSpec is not null)),
            AverageScore: Average(profile.Select(e => (double?)e.Score)),
            ScoreHistogram: Histogram(profile.Select(e => e.Score).OfType<int>().Select(s => (double)s).ToList(), 10),
            AverageMythicScore: Average(profile.Where(e => e.MythicScore > 0).Select(e => e.MythicScore)),
            MedianMythicScore: Percentile(profile.Select(e => e.MythicScore).OfType<double>().Where(s => s > 0).ToList(), 0.5),
            AverageProblems: Average(profile.Select(e => (double?)e.FindingsProblem)),
            AverageImprovements: Average(profile.Select(e => (double?)e.FindingsImprove)));

        var areas = AreaNames
            .Select(a => new AreaVerdicts(a,
                profile.Count(e => e.Areas?.GetValueOrDefault(a) == "Good"),
                profile.Count(e => e.Areas?.GetValueOrDefault(a) == "Improve"),
                profile.Count(e => e.Areas?.GetValueOrDefault(a) == "Problem")))
            .Where(a => a.Total > 0)
            .ToList();

        var slots = SlotNames
            .Select(s => new SlotItemLevel(s, Average(profile.Select(e => e.SlotItemLevels is { } d && d.TryGetValue(s, out var v) ? (double?)v : null))))
            .Where(s => s.Average is not null)
            .ToList();

        var classes = profile.GroupBy(e => (e.ClassId, e.ClassName))
            .Select(g => new ClassUsage(g.Key.ClassId, g.Key.ClassName, g.Count(), Average(g.Select(e => e.ItemLevel)), Average(g.Select(e => (double?)e.Score))))
            .OrderByDescending(c => c.Count)
            .ToList();

        var specs = profile.GroupBy(e => (e.App, e.SpecSlug, e.SpecName, e.ClassId, e.ClassName))
            .Select(g => new SpecUsage(
                g.Key.App, g.Key.SpecSlug ?? "", g.Key.SpecName ?? "?", g.Key.ClassId, g.Key.ClassName,
                ok.Count(e => e.App == g.Key.App && e.SpecSlug == g.Key.SpecSlug),
                g.Count(),
                Average(g.Select(e => e.ItemLevel)),
                Average(g.Select(e => e.ItemLevel - e.TopItemLevel)),
                Average(g.Select(e => (double?)e.Score)),
                Average(g.Select(e => e.Haste)), Average(g.Select(e => e.Crit)), Average(g.Select(e => e.Mastery)), Average(g.Select(e => e.Versatility)),
                Average(g.Select(e => e.TalentSimilarity)),
                Average(g.Where(e => e.MythicScore > 0).Select(e => e.MythicScore))))
            .OrderByDescending(s => s.Characters)
            .ToList();

        var topUsers = events.GroupBy(e => e.BattleNetId)
            .Select(g => new UserUsage(
                g.Key, g.Count(), g.Select(CharacterKey).Distinct().Count(),
                g.Select(e => e.App).Distinct().Order().ToList(),
                g.Max(e => e.OccurredAtUtc),
                firstSeen.TryGetValue(g.Key, out var first) ? first : g.Min(e => e.OccurredAtUtc),
                g.MaxBy(e => e.OccurredAtUtc)!.CharacterName))
            .OrderByDescending(u => u.Benches)
            .Take(25)
            .ToList();

        return new UsageReport(
            kpis, daily, hourly, weekHour, apps,
            Shares(events, e => e.Status),
            profile.Count, itemLevel, stats, gear, areas, slots, classes, specs,
            Shares(events, e => e.Region.ToUpperInvariant()),
            Shares(profile, e => $"{e.RealmName} ({e.Region.ToUpperInvariant()})", 15),
            Shares(profile, e => e.Faction ?? "?"),
            Shares(profile, e => e.Race ?? "?", 15),
            Shares(profile.Where(e => e.HeroTree is not null).ToList(), e => e.HeroTree!, 15),
            topUsers,
            events.OrderByDescending(e => e.OccurredAtUtc).Take(60).ToList());
    }

    private static string CharacterKey(BenchEvent e) => $"{e.Region}/{e.RealmSlug}/{e.CharacterName}".ToLowerInvariant();

    private static StatSummary Stat(string name, IReadOnlyList<BenchEvent> profile, Func<BenchEvent, double?> percent, Func<BenchEvent, double?> top, Func<BenchEvent, double?> rating) =>
        new(name, Average(profile.Select(percent)), Average(profile.Select(top)), Average(profile.Select(rating)),
            Average(profile.Where(e => percent(e) is not null && top(e) is not null).Select(e => percent(e) - top(e))));

    private static IReadOnlyList<Share> Shares(IReadOnlyCollection<BenchEvent> events, Func<BenchEvent, string> key, int take = int.MaxValue)
    {
        var total = events.Count;
        return events.GroupBy(key)
            .Select(g => new Share(g.Key, g.Count(), Ratio(g.Count(), total) ?? 0))
            .OrderByDescending(s => s.Count)
            .Take(take)
            .ToList();
    }

    internal static double? Average(IEnumerable<double?> values)
    {
        var list = values.OfType<double>().ToList();
        return list.Count == 0 ? null : list.Average();
    }

    private static double? Average(IReadOnlyCollection<double> values) => values.Count == 0 ? null : values.Average();

    private static double? Ratio(int part, int total) => total == 0 ? null : (double)part / total;

    /// <summary>Percentile par interpolation linéaire entre les deux rangs voisins.</summary>
    internal static double? Percentile(IReadOnlyCollection<double> values, double p)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var sorted = values.Order().ToArray();
        var rank = p * (sorted.Length - 1);
        var low = (int)Math.Floor(rank);
        var high = (int)Math.Ceiling(rank);
        return sorted[low] + (sorted[high] - sorted[low]) * (rank - low);
    }

    /// <summary>Classes de largeur <paramref name="width"/>, alignées sur ses multiples, vides comprises entre la première et la dernière.</summary>
    internal static IReadOnlyList<Bin> Histogram(IReadOnlyCollection<double> values, int width)
    {
        if (values.Count == 0)
        {
            return [];
        }

        var counts = values.GroupBy(v => (int)Math.Floor(v / width) * width).ToDictionary(g => g.Key, g => g.Count());
        var bins = new List<Bin>();
        for (var low = counts.Keys.Min(); low <= counts.Keys.Max(); low += width)
        {
            bins.Add(new Bin(low, low + width, counts.GetValueOrDefault(low)));
        }

        return bins;
    }
}

/// <param name="SuccessRate">Part des benchs aboutis (0 à 1).</param>
/// <param name="CacheRate">Part des benchs servis par le cache du site.</param>
/// <param name="ForcedRate">Part des benchs relancés par « Actualiser ».</param>
/// <param name="AverageDurationMs">Durée moyenne d'un bench réellement calculé (hors cache).</param>
/// <param name="ReturningUsers">Comptes venus au moins deux jours différents dans la période.</param>
public sealed record UsageKpis(
    int Benches,
    int Users,
    int Characters,
    int NewUsers,
    int ReturningUsers,
    double? SuccessRate,
    double? CacheRate,
    double? ForcedRate,
    double? AverageDurationMs,
    double? P95DurationMs,
    double? BenchesPerUser);

public sealed record DailyUsage(DateOnly Day, IReadOnlyDictionary<string, int> ByApp, int Users)
{
    public int Total => ByApp.Values.Sum();
}

public sealed record AppUsage(string App, int Benches, int Users, int Characters, double? AverageItemLevel);

public sealed record Share(string Label, int Count, double Ratio);

public sealed record Bin(int Low, int High, int Count);

/// <param name="AverageGap">Écart moyen au niveau d'objet médian du top de la spécialisation (négatif : en dessous).</param>
public sealed record ItemLevelSummary(
    double? Average,
    double? Median,
    double? Min,
    double? Max,
    double? P25,
    double? P75,
    double? AverageGap,
    IReadOnlyList<Bin> Histogram,
    int Sample);

/// <param name="AverageGap">Écart moyen au pourcentage médian du top, en points.</param>
public sealed record StatSummary(string Name, double? AveragePercent, double? AverageTopPercent, double? AverageRating, double? AverageGap);

/// <param name="GoodPieceRate">Part moyenne des pièces jugées « bonnes » face au top (0 à 1).</param>
/// <param name="TopShare">Popularité moyenne, dans le top, des pièces portées.</param>
/// <param name="EnchantCoverage">Enchantements posés / enchantements attendus, sur tout l'échantillon.</param>
/// <param name="SocketFill">Châsses garnies / châsses.</param>
public sealed record GearSummary(
    double? GoodPieceRate,
    double? ProblemPieceRate,
    double? TopShare,
    double? EnchantCoverage,
    double? FullyEnchantedRate,
    double? SocketFill,
    double? AverageSockets,
    double? AverageEmbellishments,
    double? TalentSimilarity,
    double? TopHeroTreeRate,
    double? PlaysSpecRate,
    double? AverageScore,
    IReadOnlyList<Bin> ScoreHistogram,
    double? AverageMythicScore,
    double? MedianMythicScore,
    double? AverageProblems,
    double? AverageImprovements);

public sealed record AreaVerdicts(string Area, int Good, int Improve, int Problem)
{
    public int Total => Good + Improve + Problem;
}

public sealed record SlotItemLevel(string Slot, double? Average);

public sealed record ClassUsage(int ClassId, string ClassName, int Count, double? AverageItemLevel, double? AverageScore);

/// <param name="Benches">Benchs aboutis de la spécialisation ; <paramref name="Characters"/> : personnages de l'échantillon de profil.</param>
public sealed record SpecUsage(
    string App,
    string Slug,
    string SpecName,
    int ClassId,
    string ClassName,
    int Benches,
    int Characters,
    double? AverageItemLevel,
    double? AverageGap,
    double? AverageScore,
    double? Haste,
    double? Crit,
    double? Mastery,
    double? Versatility,
    double? TalentSimilarity,
    double? MythicScore);

public sealed record UserUsage(
    long BattleNetId,
    int Benches,
    int Characters,
    IReadOnlyList<string> Apps,
    DateTimeOffset LastSeenUtc,
    DateTimeOffset FirstSeenUtc,
    string LastCharacter);
