using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Meta;

namespace Noovio.Pulse.Web.Components.Analysis;

public partial class MythicPlusPanel
{
    [Parameter] public MythicPlusProfile? MythicPlus { get; set; }

    /// <summary>Même repère que l'analyse : un donjon 2 niveaux sous la médiane des meilleures clés est « en retard ».</summary>
    private double Median => MythicPlus is null ? 0 : MetaAggregatorService.Median(MythicPlus.BestRuns.Select(r => (double)r.Level));
}
