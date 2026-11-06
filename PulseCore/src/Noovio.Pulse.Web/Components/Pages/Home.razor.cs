using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Entities;

namespace Noovio.Pulse.Web.Components.Pages;

public partial class Home
{
    [Inject] private ISpecMetaQueries Queries { get; set; } = default!;
    [Inject] private IDungeonRouteQueries RouteQueries { get; set; } = default!;

    /// <summary>Vide si l'application n'a pas de routes, ou avant leur première synchronisation.</summary>
    private IReadOnlyList<SeasonDungeon> dungeons = [];

    /// <summary>Les spécialisations du rôle dans leur ordre, snapshot null avant la première ingestion.</summary>
    private List<(SpecInfo Info, SpecMetaSnapshot? Meta)> specs = [];

    private string? season;
    private DateTimeOffset? lastUpdate;
    private int totalPlayers;
    private int medianKey;
    private double medianItemLevel;
    private (SpecInfo Info, TopPlayer Player)? bestRun;

    protected override async Task OnInitializedAsync()
    {
        if (Site.HasDungeonRoutes)
        {
            dungeons = await RouteQueries.GetCurrentAsync();
        }

        // Lus un par un : les snapshots sont en cache mémoire, et le DbContext n'accepte pas de requêtes parallèles.
        foreach (var info in Site.Role.Specs)
        {
            specs.Add((info, await Queries.GetLatestAsync(info.Spec)));
        }

        var metas = specs.Where(s => s.Meta is not null).Select(s => s.Meta!).ToList();
        if (metas.Count == 0)
        {
            return;
        }

        season = metas.MaxBy(m => m.ComputedAtUtc)!.Season;
        lastUpdate = metas.Max(m => m.ComputedAtUtc);
        totalPlayers = metas.Sum(m => m.SampleSize);
        medianKey = (int)Median(metas.SelectMany(m => m.Players).Select(p => (double)p.BestKeyLevel));
        medianItemLevel = Median(metas.SelectMany(m => m.Players).Select(p => p.ItemLevel).Where(l => l > 0));
        bestRun = specs
            .SelectMany(s => s.Meta?.Players.Select(p => (s.Info, Player: p)) ?? [])
            .OrderByDescending(x => x.Player.BestKeyLevel)
            .ThenByDescending(x => x.Player.BestRunScore)
            .Select(x => ((SpecInfo, TopPlayer)?)x)
            .FirstOrDefault();
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToList();
        return sorted.Count == 0 ? 0
            : sorted.Count % 2 == 1 ? sorted[sorted.Count / 2]
            : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;
    }
}
