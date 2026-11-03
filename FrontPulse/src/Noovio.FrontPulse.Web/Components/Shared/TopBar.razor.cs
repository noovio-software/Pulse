using Microsoft.AspNetCore.Components;
using Noovio.FrontPulse.Domain.Enums;

namespace Noovio.FrontPulse.Web.Components.Shared;

public partial class TopBar
{
    [Inject] private RefreshSchedule Schedule { get; set; } = default!;

    /// <summary>Spécialisation surlignée dans la navigation.</summary>
    [Parameter] public TankSpec? Active { get; set; }

    /// <summary>Actions propres à la page, alignées à droite de l'en-tête.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private DateTimeOffset? nextRefresh;

    protected override async Task OnInitializedAsync() => nextRefresh = await Schedule.GetNextAsync();
}
