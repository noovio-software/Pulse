using Microsoft.AspNetCore.Components;
using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Application.Meta;

namespace Noovio.FrontPulse.Web.Components.Analysis;

public partial class MythicPlusPanel
{
    [Parameter] public MythicPlusProfile? MythicPlus { get; set; }

    /// <summary>Même repère que l'analyse : un donjon 2 niveaux sous la médiane des meilleures clés est « en retard ».</summary>
    private double Median => MythicPlus is null ? 0 : TankMetaAggregatorService.Median(MythicPlus.BestRuns.Select(r => (double)r.Level));
}
