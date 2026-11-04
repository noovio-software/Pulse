using Microsoft.AspNetCore.Components;
using Noovio.FrontPulse.Application.Analysis;
using Noovio.FrontPulse.Domain.Enums;

namespace Noovio.FrontPulse.Web.Components.Analysis;

public partial class GemEnchantPanel
{
    [Parameter, EditorRequired] public GemEnchantReview Review { get; set; } = default!;

    private bool HasBreakdown => Review.Breakdown.Any(b => b.Items + b.Gems + b.Enchants > 0);

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
}
