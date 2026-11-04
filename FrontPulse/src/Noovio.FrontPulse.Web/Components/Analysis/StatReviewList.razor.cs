using System.Globalization;
using Microsoft.AspNetCore.Components;
using Noovio.FrontPulse.Application.Analysis;

namespace Noovio.FrontPulse.Web.Components.Analysis;

public partial class StatReviewList
{
    /// <summary>Triées par priorité du top.</summary>
    [Parameter, EditorRequired] public IReadOnlyList<StatReview> Stats { get; set; } = [];

    /// <summary>Effet estimé des gemmes et enchantements proposés.</summary>
    [Parameter] public IReadOnlyList<StatProjection> Projection { get; set; } = [];

    /// <summary>Ce que le joueur doit faire de cette caractéristique, en quelques mots.</summary>
    private static string Action(StatReview s) =>
        s.Percent < s.Top.P25Percent ? $"À monter de {s.Top.P25Percent - s.Percent:0.#} %"
        : s.Percent > s.Top.P75Percent ? $"En surplus de {s.Percent - s.Top.P75Percent:0.#} %"
        : "Dans la fourchette";

    /// <summary>Réglette commune : de 0 au plus haut des valeurs affichées, arrondi à la dizaine supérieure.</summary>
    private double ScaleMax => Math.Max(10, Math.Ceiling(Stats.Max(s => Math.Max(s.Percent, s.Top.P75Percent)) * 1.05 / 10) * 10);

    private string Pos(double percent) => Css(Math.Clamp(percent / ScaleMax, 0, 1));

    private string Width(StatReview s) => Css(Math.Max(0.01, (s.Top.P75Percent - s.Top.P25Percent) / ScaleMax));

    private static string Css(double fraction) => (fraction * 100).ToString("0.##", CultureInfo.InvariantCulture) + "%";
}
