using Microsoft.AspNetCore.Components;
using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Domain;
using Noovio.FrontPulse.Domain.Entities;

namespace Noovio.FrontPulse.Web.Components.Pages;

public partial class Home
{
    [Inject] private ISpecMetaQueries Queries { get; set; } = default!;

    /// <summary>Les six spécialisations dans l'ordre de <see cref="TankSpecs.All"/>, snapshot null avant la première ingestion.</summary>
    private List<(TankSpecInfo Info, SpecMetaSnapshot? Meta)> specs = [];

    private string? season;
    private DateTimeOffset? lastUpdate;
    private int totalTanks;
    private int medianKey;
    private double medianItemLevel;
    private (TankSpecInfo Info, TopTank Tank)? bestRun;

    protected override async Task OnInitializedAsync()
    {
        // Lus un par un : les snapshots sont en cache mémoire, et le DbContext n'accepte pas de requêtes parallèles.
        foreach (var info in TankSpecs.All)
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
        totalTanks = metas.Sum(m => m.SampleSize);
        medianKey = (int)Median(metas.SelectMany(m => m.Players).Select(p => (double)p.BestKeyLevel));
        medianItemLevel = Median(metas.SelectMany(m => m.Players).Select(p => p.ItemLevel).Where(l => l > 0));
        bestRun = specs
            .SelectMany(s => s.Meta?.Players.Select(p => (s.Info, Tank: p)) ?? [])
            .OrderByDescending(x => x.Tank.BestKeyLevel)
            .ThenByDescending(x => x.Tank.BestRunScore)
            .Select(x => ((TankSpecInfo, TopTank)?)x)
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
