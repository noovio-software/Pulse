using Microsoft.AspNetCore.Components;
using Noovio.FrontPulse.Domain.Entities;

namespace Noovio.FrontPulse.Web.Components.Analysis;

public partial class TalentChips
{
    [Parameter, EditorRequired] public IReadOnlyList<TalentPickRate> Talents { get; set; } = [];

    /// <summary>Arbre de la spécialisation, pour retrouver l'icône de chaque talent.</summary>
    [Parameter] public TalentTreeLayout? Layout { get; set; }

    private string? Icon(TalentPickRate talent) =>
        Layout?.Sections
            .Where(s => s.Tree == talent.Tree)
            .SelectMany(s => s.Nodes)
            .SelectMany(n => n.Choices)
            .FirstOrDefault(c => c.TalentId == talent.TalentId)?.IconUrl;
}
