using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Application.Analysis;

namespace Noovio.Pulse.Web.Components.Analysis;

public partial class GearReviewList
{
    [Parameter, EditorRequired] public IReadOnlyList<GearReview> Gear { get; set; } = [];
}
