using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace Noovio.FrontPulse.Web.Components.Analysis;

public partial class ScoreRing
{
    private const double Circumference = 2 * Math.PI * 52;

    [Parameter, EditorRequired] public int Score { get; set; }
    [Parameter] public int Size { get; set; } = 132;

    private string Dash =>
        string.Create(CultureInfo.InvariantCulture, $"{Circumference * Math.Clamp(Score, 0, 100) / 100:0.#} {Circumference:0.#}");

    private string Color => Score switch
    {
        >= 75 => "#34D399",
        >= 50 => "#FBBF24",
        _ => "#F87171"
    };
}
