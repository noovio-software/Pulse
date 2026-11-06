using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Domain.Entities;

namespace Noovio.Pulse.Web.Components.Pages;

public partial class RouteIndex
{
    [Inject] private IDungeonRouteQueries Queries { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private IReadOnlyList<SeasonDungeon> dungeons = [];
    private DateTimeOffset? lastUpdate;

    protected override async Task OnInitializedAsync()
    {
        if (!Site.HasDungeonRoutes)
        {
            Navigation.NotFound();
            return;
        }

        dungeons = await Queries.GetCurrentAsync();
        lastUpdate = dungeons.Count > 0 ? dungeons.Max(d => d.FetchedAtUtc) : null;
    }
}
