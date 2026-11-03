using Microsoft.AspNetCore.Components;
using Noovio.FrontPulse.Application.Analysis;

namespace Noovio.FrontPulse.Web.Components.Analysis;

public partial class GearReviewList
{
    [Parameter, EditorRequired] public IReadOnlyList<GearReview> Gear { get; set; } = [];
}
