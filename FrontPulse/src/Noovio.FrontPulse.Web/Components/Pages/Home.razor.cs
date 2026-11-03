using Microsoft.AspNetCore.Components;
using Noovio.FrontPulse.Application.Abstractions;

namespace Noovio.FrontPulse.Web.Components.Pages;

public partial class Home
{
    [Inject] private ISpecMetaQueries Queries { get; set; } = default!;

    private IReadOnlyList<SpecMetaSummary> summaries = [];

    protected override async Task OnInitializedAsync() => summaries = await Queries.GetSummariesAsync();
}
