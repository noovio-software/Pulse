using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Domain.Entities;

namespace Noovio.Pulse.Web.Components.Meta;

public partial class ItemUsage
{
    [Parameter, EditorRequired] public ItemStatSnapshot Item { get; set; } = default!;

    /// <summary>Vignette d'icône, pour les alternatives sous l'objet principal d'un emplacement.</summary>
    [Parameter] public bool Chip { get; set; }
}
