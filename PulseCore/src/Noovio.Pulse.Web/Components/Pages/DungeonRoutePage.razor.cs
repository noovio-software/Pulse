using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Routes;
using Noovio.Pulse.Domain.Entities;

namespace Noovio.Pulse.Web.Components.Pages;

public partial class DungeonRoutePage
{
    [Inject] private IDungeonRouteQueries Queries { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Parameter] public string Slug { get; set; } = "";

    /// <summary>Route ouverte ; à défaut, la plus populaire. Dans l'adresse, pour qu'elle se partage.</summary>
    [SupplyParameterFromQuery(Name = "route")] public string? RouteKey { get; set; }

    private IReadOnlyList<SeasonDungeon> dungeons = [];
    private SeasonDungeon? dungeon;
    private DungeonRoute? selected;
    private IReadOnlyDictionary<string, IReadOnlyList<RouteHighlight>> highlights = new Dictionary<string, IReadOnlyList<RouteHighlight>>();

    protected override async Task OnParametersSetAsync()
    {
        if (!Site.HasDungeonRoutes)
        {
            Navigation.NotFound();
            return;
        }

        dungeons = await Queries.GetCurrentAsync();
        dungeon = dungeons.FirstOrDefault(d => string.Equals(d.Slug, Slug, StringComparison.OrdinalIgnoreCase));
        if (dungeon is null)
        {
            return;
        }

        selected = dungeon.Routes.FirstOrDefault(r => r.PublicKey == RouteKey) ?? dungeon.Routes.FirstOrDefault();
        highlights = RouteSelection.Highlights(dungeon.Routes);
    }
}
