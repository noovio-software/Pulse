using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Domain.Enums;

namespace Noovio.Pulse.Web.Components.Shared;

public partial class TopBar
{
    [Inject] private RefreshSchedule Schedule { get; set; } = default!;

    /// <summary>Spécialisation surlignée dans la navigation.</summary>
    [Parameter] public WowSpec? Active { get; set; }

    /// <summary>Lien « Routes » surligné dans la navigation.</summary>
    [Parameter] public bool RoutesActive { get; set; }

    /// <summary>Actions propres à la page, alignées à droite de l'en-tête.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private DateTimeOffset? nextRefresh;

    protected override async Task OnInitializedAsync() => nextRefresh = await Schedule.GetNextAsync();
}
