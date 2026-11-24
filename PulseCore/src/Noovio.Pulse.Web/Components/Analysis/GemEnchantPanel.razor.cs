using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Application.Analysis;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Enums;
using Noovio.Pulse.Web.Resources;

namespace Noovio.Pulse.Web.Components.Analysis;

public partial class GemEnchantPanel
{
    [Parameter, EditorRequired] public GemEnchantReview Review { get; set; } = default!;

    private bool HasBreakdown => Review.Breakdown.Any(b => b.Items + b.Gems + b.Enchants > 0);

    /// <summary>
    /// « À faire » : d'abord ce qui manque (châsse vide, enchantement attendu absent) et que le plan d'optimisation ne
    /// couvre pas déjà, avec le choix le plus courant du top quand il est connu ; puis les changements du plan.
    /// </summary>
    private IReadOnlyList<ToDoRow> ToDo
    {
        get
        {
            var topGem = Review.TopGems.FirstOrDefault();
            var emptySockets = Review.Sockets
                .Where(s => s.GemId is null && s.Swap is null)
                .Select(s => new ToDoRow(s.Slot, s.ItemName, WebTexts.Gems_EmptySocket, topGem?.Name, topGem?.IconUrl, []));
            var missingEnchants = Review.Enchants
                .Where(e => e.Label is null && e.Swap is null)
                .Select(e => new ToDoRow(e.Slot, e.ItemName,
                    GearRules.TakesLegAugment(e.Slot) ? WebTexts.Gear_NoLegAugment : WebTexts.Gear_NoEnchant, e.TopChoice, null, []));
            var swaps = Review.Swaps
                .Select(s => new ToDoRow(s.Slot, s.ItemName, null, s.To, s.ToIconUrl, s.Deltas, s.From));

            return missingEnchants.Concat(emptySockets).OrderBy(r => r.Slot).Concat(swaps).ToList();
        }
    }

    /// <summary>Une ligne par pièce portant une châsse ou un enchantement, les pièces à corriger d'abord.</summary>
    private IReadOnlyList<PieceRow> Rows => Review.Sockets.Select(s => (s.Slot, s.ItemId))
        .Concat(Review.Enchants.Select(e => (e.Slot, e.ItemId)))
        .Distinct()
        .Select(piece =>
        {
            var sockets = Review.Sockets.Where(s => s.ItemId == piece.ItemId && s.Slot == piece.Slot).ToList();
            var enchant = Review.Enchants.FirstOrDefault(e => e.ItemId == piece.ItemId && e.Slot == piece.Slot);
            var verdicts = sockets.Select(s => s.Verdict).Concat(enchant is null ? [] : [enchant.Verdict]);
            return new PieceRow(piece.Slot, sockets, enchant, verdicts.Max());
        })
        .OrderByDescending(r => r.Verdict)
        .ThenBy(r => r.Slot)
        .ToList();

    private sealed record PieceRow(GearSlot Slot, IReadOnlyList<SocketReview> Sockets, EnchantReview? Enchant, Verdict Verdict);

    /// <param name="Missing">Ce qui manque (« Châsse vide »), ou null pour un changement du plan.</param>
    /// <param name="To">Ce qu'il faut poser ; null quand le top n'indique aucun choix.</param>
    /// <param name="From">Ce qui est remplacé, pour un changement du plan.</param>
    private sealed record ToDoRow(
        GearSlot Slot, string ItemName, string? Missing, string? To, string? IconUrl, IReadOnlyList<StatDelta> Deltas, string? From = null);
}
