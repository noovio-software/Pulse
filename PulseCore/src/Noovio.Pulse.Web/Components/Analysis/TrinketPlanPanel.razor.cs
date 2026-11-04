using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Analysis;
using Noovio.Pulse.Domain.Enums;
using Noovio.Pulse.Web.Components.Shared;

namespace Noovio.Pulse.Web.Components.Analysis;

public partial class TrinketPlanPanel
{
    [Parameter, EditorRequired] public TrinketPlan Plan { get; set; } = default!;

    private IReadOnlyList<TrinketAdvice> ToReplace => Plan.Trinkets.Where(t => t.ReplaceWith is not null).ToList();

    /// <summary>Un bijou du rôle à activer, porté ou visé : les moments de boss deviennent alors utiles pour lui.</summary>
    private bool HasRoleTrinket => Plan.Trinkets.Any(t => (t.ReplaceWith?.Usage ?? t.Usage) is { OnUse: true, Orientation: TrinketOrientation.Role });

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

    private string Advice(TrinketUsage usage) => (usage.OnUse, usage.Orientation) switch
    {
        (false, _) => "effet automatique, rien à déclencher.",
        (true, TrinketOrientation.Role) => string.Format(Site.Words.RoleTrinketAdvice, Recharge(usage)),
        _ => string.Format(Site.Words.DamageTrinketAdvice, Recharge(usage))
    };

    private static string Recharge(TrinketUsage usage) => usage.Recharge is null ? "" : $" ({usage.Recharge})";
}
