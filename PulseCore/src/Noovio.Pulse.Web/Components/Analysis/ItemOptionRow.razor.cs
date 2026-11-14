using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Application.Analysis;

namespace Noovio.Pulse.Web.Components.Analysis;

public partial class ItemOptionRow
{
    [Parameter, EditorRequired] public ItemOption Option { get; set; } = default!;

    /// <summary>Infobulle Wowhead de la pièce exacte : niveau d'objet et bonus (qualité, châsse, piste d'amélioration).</summary>
    private string Wowhead =>
        $"item={Option.ItemId}&ilvl={Option.ItemLevel}" + (Option.Bonus.Count > 0 ? $"&bonus={string.Join(':', Option.Bonus)}" : "") + "&domain=fr";
}
