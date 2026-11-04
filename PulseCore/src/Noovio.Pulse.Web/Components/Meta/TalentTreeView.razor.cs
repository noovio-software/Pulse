using System.Globalization;
using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Web.Components.Shared;

namespace Noovio.Pulse.Web.Components.Meta;

public partial class TalentTreeView
{
    /// <summary>Taille d'une case de la grille en jeu, en pixels.</summary>
    private const double Cell = 52;
    private const double NodeSize = 38;
    private const double Offset = (Cell - NodeSize) / 2;

    [Parameter, EditorRequired] public TalentTreeSection Section { get; set; } = default!;

    /// <summary>Talents pris par un joueur : active la comparaison joueur / top au lieu de la vue méta.</summary>
    [Parameter] public IReadOnlyCollection<int>? Selected { get; set; }

    private IEnumerable<(TalentNodeView From, TalentNodeView To)> Links
    {
        get
        {
            var byId = Section.Nodes.ToDictionary(n => n.NodeId);
            return Section.Nodes.SelectMany(n => n.Unlocks.Where(byId.ContainsKey).Select(id => (n, byId[id])));
        }
    }

    /// <summary>Un nœud à choix est pris si l'une de ses options l'est : les parts s'additionnent.</summary>
    private static double Share(TalentNodeView node) => Math.Min(1, node.Choices.Sum(c => c.Share));

    private bool IsPicked(TalentNodeView node) => Selected is not null && node.Choices.Any(c => Selected.Contains(c.TalentId));

    /// <summary>En mode joueur, l'option affichée est celle du joueur ; sinon la plus prise du top.</summary>
    private TalentChoiceView Shown(TalentNodeView node) =>
        (Selected is null ? null : node.Choices.FirstOrDefault(c => Selected.Contains(c.TalentId)))
        ?? node.Choices.OrderByDescending(c => c.Share).First();

    private string Tier(TalentNodeView node)
    {
        if (Selected is null)
        {
            return Ui.PickTier(Share(node));
        }

        var share = Share(node);
        return IsPicked(node)
            ? share >= 0.5 ? "is-match" : share < 0.1 ? "is-atypical" : "is-picked"
            : share >= 0.9 ? "is-missing" : "is-unpicked";
    }

    /// <summary>Lien allumé entre deux nœuds pris (par la majorité du top, ou par le joueur).</summary>
    private bool IsLit(TalentNodeView node) => Selected is null ? Share(node) >= 0.5 : IsPicked(node);

    private static string Center(double gridPosition) =>
        (gridPosition * Cell + Cell / 2).ToString("0.#", CultureInfo.InvariantCulture);

    private static string Label(TalentNodeView node) =>
        string.Join(" / ", node.Choices.Select(c => $"{c.Name} {Ui.Percent(c.Share)}"));
}
