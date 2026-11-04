using Microsoft.AspNetCore.Components;
using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Application.Analysis;
using Noovio.FrontPulse.Domain.Enums;
using Noovio.FrontPulse.Web.Components.Shared;

namespace Noovio.FrontPulse.Web.Components.Analysis;

public partial class TrinketPlanPanel
{
    [Parameter, EditorRequired] public TrinketPlan Plan { get; set; } = default!;

    private IReadOnlyList<TrinketAdvice> ToReplace => Plan.Trinkets.Where(t => t.ReplaceWith is not null).ToList();

    /// <summary>Un bijou défensif à activer, porté ou visé : les coups de boss deviennent alors utiles pour lui.</summary>
    private bool HasDefensive => Plan.Trinkets.Any(t => (t.ReplaceWith?.Usage ?? t.Usage) is { OnUse: true, Orientation: TrinketOrientation.Defensive });

    private string Headline => ToReplace.Count switch
    {
        0 when Plan.Trinkets.Count >= 2 => "Garde tes deux bijoux : le top joue les mêmes.",
        0 => "Garde ce bijou : le top le joue.",
        1 => $"Un bijou à changer : {ToReplace[0].Name} → {ToReplace[0].ReplaceWith!.Name}.",
        _ => $"Deux bijoux à changer : vise {string.Join(" et ", ToReplace.Select(t => t.ReplaceWith!.Name))}."
    };

    private static string Source(TrinketRef target) => target.Boss is null
        ? "Source non listée dans le journal"
        : $"Tombe sur {target.Boss}, {target.Instance} ({Ui.InstanceKindLabel(target.Kind!.Value).ToLowerInvariant()})";

    private static string Advice(TrinketUsage usage) => (usage.OnUse, usage.Orientation) switch
    {
        (false, _) => "effet automatique, rien à déclencher.",
        (true, TrinketOrientation.Defensive) => $"à activer juste avant les gros coups{Recharge(usage)}, plutôt qu'à chaque recharge.",
        _ => $"à activer dès qu'il est prêt{Recharge(usage)}, avec tes temps de recharge offensifs, sur un boss ou un gros pack."
    };

    private static string Recharge(TrinketUsage usage) => usage.Recharge is null ? "" : $" ({usage.Recharge})";
}
