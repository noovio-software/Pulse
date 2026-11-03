using Microsoft.AspNetCore.Components;
using Noovio.FrontPulse.Domain.Entities;

namespace Noovio.FrontPulse.Web.Components.Meta;

public partial class ItemUsage
{
    [Parameter, EditorRequired] public ItemStatSnapshot Item { get; set; } = default!;

    /// <summary>Vignette d'icône, pour les alternatives sous l'objet principal d'un emplacement.</summary>
    [Parameter] public bool Chip { get; set; }
}
