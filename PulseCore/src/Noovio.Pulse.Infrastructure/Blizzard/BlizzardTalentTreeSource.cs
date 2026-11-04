using System.Text.RegularExpressions;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Enums;

namespace Noovio.Pulse.Infrastructure.Blizzard;

/// <summary>
/// Arbre de talents d'une spécialisation (Game Data API) : positions des nœuds, liens, options et icônes.
/// L'identifiant d'arbre (un par classe) se lit dans l'index, dans le lien de chaque spécialisation.
/// </summary>
public sealed partial class BlizzardTalentTreeSource(BlizzardClient client, IMediaSource media) : ITalentTreeSource
{
    /// <summary>Une colonne ou une rangée de l'arbre en jeu vaut 600 unités de position brute.</summary>
    private const double GridUnit = 600;

    public async Task<TalentTreeLayout?> GetLayoutAsync(WowSpec spec, CancellationToken ct)
    {
        var index = await client.GetTalentTreeIndexAsync(ct);
        var treeId = index?.SpecTalentTrees
            .Select(e => TreeHref().Match(e.Key.Href))
            .Where(m => m.Success && int.Parse(m.Groups["spec"].Value) == (int)spec)
            .Select(m => int.Parse(m.Groups["tree"].Value))
            .FirstOrDefault();

        if (treeId is null or 0 || await client.GetTalentTreeAsync(treeId.Value, (int)spec, ct) is not { } tree)
        {
            return null;
        }

        var spellIds = tree.ClassTalentNodes
            .Concat(tree.SpecTalentNodes)
            .Concat(tree.HeroTalentTrees.SelectMany(h => h.HeroTalentNodes))
            .SelectMany(Options)
            .Select(o => o.SpellTooltip?.Spell?.Id ?? 0)
            .Where(id => id > 0);
        var icons = await media.GetSpellIconsAsync(spellIds, ct);

        // L'API range aussi les nœuds héroïques parmi ceux de la spécialisation, à leurs propres positions :
        // sans ce filtre, l'arbre de spécialisation s'élargit de deux grappes grises (les arbres héroïques).
        var heroNodeIds = tree.HeroTalentTrees.SelectMany(h => h.HeroTalentNodes).Select(n => n.Id).ToHashSet();
        List<BzTalentNode> WithoutHero(IEnumerable<BzTalentNode> nodes) => nodes.Where(n => !heroNodeIds.Contains(n.Id)).ToList();

        var sections = new List<TalentTreeSection>
        {
            Section(TalentTree.Class, tree.PlayableClass?.Name ?? spec.Info().ClassName, null, WithoutHero(tree.ClassTalentNodes), icons),
            Section(TalentTree.Spec, tree.Name ?? spec.Info().SpecName, spec.Info().IconUrl, WithoutHero(tree.SpecTalentNodes), icons)
        };

        // Le média des arbres héroïques répond 404 : l'icône est celle du talent d'entrée (en haut de l'arbre).
        sections.AddRange(tree.HeroTalentTrees.Select(h =>
        {
            var section = Section(TalentTree.Hero, h.Name, null, h.HeroTalentNodes, icons);
            var entry = section.Nodes.OrderBy(n => n.Y).ThenBy(n => n.X).FirstOrDefault();
            return section with { IconUrl = entry?.Choices.FirstOrDefault()?.IconUrl };
        }));

        return new TalentTreeLayout(sections);
    }

    private static TalentTreeSection Section(TalentTree kind, string name, string? iconUrl, List<BzTalentNode> nodes, IReadOnlyDictionary<int, string> icons)
    {
        var usable = nodes.Where(n => Options(n).Any()).ToList();
        if (usable.Count == 0)
        {
            return new TalentTreeSection(kind, name, iconUrl, 0, 0, 0, []);
        }

        var minX = usable.Min(n => n.RawPositionX);
        var minY = usable.Min(n => n.RawPositionY);
        var ids = usable.Select(n => n.Id).ToHashSet();

        var views = usable.Select(n => new TalentNodeView(
                n.Id,
                (n.RawPositionX - minX) / GridUnit,
                (n.RawPositionY - minY) / GridUnit,
                n.NodeType?.Type switch
                {
                    "CHOICE" => TalentNodeKind.Choice,
                    "PASSIVE" => TalentNodeKind.Passive,
                    _ => TalentNodeKind.Active
                },
                Math.Max(1, n.Ranks.Count),
                n.Unlocks.Where(ids.Contains).ToList(),
                Options(n)
                    .DistinctBy(o => o.Talent?.Id)
                    .Select(o => new TalentChoiceView(
                        o.Talent?.Id ?? 0,
                        o.SpellTooltip?.Spell?.Id ?? 0,
                        o.Talent?.Name ?? o.SpellTooltip?.Spell?.Name ?? "",
                        icons.GetValueOrDefault(o.SpellTooltip?.Spell?.Id ?? 0),
                        0))
                    .ToList()))
            .ToList();

        return new TalentTreeSection(kind, name, iconUrl, 0, views.Max(v => v.X) + 1, views.Max(v => v.Y) + 1, views);
    }

    /// <summary>Options d'un nœud : l'infobulle du premier rang, ou chacune des options d'un nœud à choix.</summary>
    private static IEnumerable<BzTalentTooltip> Options(BzTalentNode node) =>
        node.Ranks.FirstOrDefault() is { } rank
            ? rank.ChoiceOfTooltips.Count > 0 ? rank.ChoiceOfTooltips : rank.Tooltip is { } t ? [t] : []
            : [];

    [GeneratedRegex(@"talent-tree/(?<tree>\d+)/playable-specialization/(?<spec>\d+)")]
    private static partial Regex TreeHref();
}
