using System.Globalization;
using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Analysis;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Enums;
using Noovio.Pulse.Web.Components.Shared;
using Noovio.Pulse.Web.Resources;

namespace Noovio.Pulse.Web.Components.Analysis;

public partial class TrinketPlanPanel
{
    [Parameter, EditorRequired] public TrinketPlan Plan { get; set; } = default!;

    private IReadOnlyList<TrinketAdvice> ToReplace => Plan.Trinkets.Where(t => t.ReplaceWith is not null).ToList();

    /// <summary>Un bijou du rôle à activer, porté ou visé : les moments de boss deviennent alors utiles pour lui.</summary>
    private bool HasRoleTrinket => Plan.Trinkets.Any(t => (t.ReplaceWith?.Usage ?? t.Usage) is { OnUse: true, Orientation: TrinketOrientation.Role });

    private string Headline => ToReplace.Count switch
    {
        0 when Plan.Trinkets.Count >= 2 => WebTexts.Trinket_KeepBoth,
        0 => WebTexts.Trinket_KeepOne,
        1 => Ui.F(WebTexts.Trinket_ChangeOne, ToReplace[0].Name, ToReplace[0].ReplaceWith!.Name),
        _ => Ui.F(WebTexts.Trinket_ChangeTwo, string.Join($" {GameLabels.And} ", ToReplace.Select(t => t.ReplaceWith!.Name)))
    };

    private static string Source(TrinketRef target) => target.Boss is null
        ? WebTexts.Trinket_SourceUnknown
        : Ui.F(WebTexts.Trinket_Source, target.Boss, target.Instance, Ui.InstanceKindLabel(target.Kind!.Value).ToLower(CultureInfo.CurrentCulture));

    private string Advice(TrinketUsage usage) => (usage.OnUse, usage.Orientation) switch
    {
        (false, _) => WebTexts.Trinket_Passive,
        (true, TrinketOrientation.Role) => Ui.F(Site.Words.RoleTrinketAdvice, Recharge(usage)),
        _ => Ui.F(Site.Words.DamageTrinketAdvice, Recharge(usage))
    };

    private static string Recharge(TrinketUsage usage) => usage.Recharge is null ? "" : $" ({usage.Recharge})";
}
