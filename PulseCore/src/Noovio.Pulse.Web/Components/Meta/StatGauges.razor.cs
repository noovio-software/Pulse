using System.Globalization;
using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Domain.Entities;

namespace Noovio.Pulse.Web.Components.Meta;

public partial class StatGauges
{
    /// <summary>Triées par priorité (part des points investis, décroissante).</summary>
    [Parameter, EditorRequired] public IReadOnlyList<StatDistribution> Stats { get; set; } = [];

    /// <summary>Réglette commune aux quatre caractéristiques : de 0 au plus haut 3e quartile, arrondi à la dizaine.</summary>
    private double ScaleMax => Math.Max(10, Math.Ceiling(Stats.Max(s => Math.Max(s.P75Percent, s.MedianPercent)) / 10) * 10);

    private string Pos(double percent) => Css(Math.Clamp(percent / ScaleMax, 0, 1));

    /// <summary>Largeur minimale pour rester visible quand tout le top est au même pourcentage.</summary>
    private string Width(StatDistribution s) => Css(Math.Max(0.01, (s.P75Percent - s.P25Percent) / ScaleMax));

    private static string Css(double fraction) => (fraction * 100).ToString("0.##", CultureInfo.InvariantCulture) + "%";

    private static string Format(double percent) => percent.ToString("0");
}
