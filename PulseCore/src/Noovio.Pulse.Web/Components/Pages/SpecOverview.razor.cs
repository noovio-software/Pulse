using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Enums;

namespace Noovio.Pulse.Web.Components.Pages;

public partial class SpecOverview
{
    [Inject] private ISpecMetaQueries Queries { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public string Slug { get; set; } = "";

    /// <summary>
    /// Relu à l'ouverture du circuit plutôt que transmis par [PersistentState] : avec l'arbre de talents et le top 100,
    /// le snapshot dépasse la taille maximale d'un message SignalR (32 Ko) et ferait tomber la connexion.
    /// La relecture vient du cache mémoire de <see cref="ISpecMetaQueries"/>, sans aller en base.
    /// </summary>
    private SpecMetaSnapshot? Meta { get; set; }

    private SpecInfo? info;
    private bool refreshTooltips;

    /// <summary>Virtualize exige une ICollection ; le snapshot expose une IReadOnlyList.</summary>
    private List<TopPlayer> players = [];

    /// <summary>Les bijoux ont leur propre section (rôle / dégâts).</summary>
    private IReadOnlyList<SlotUsage> GearSlots => Meta?.Slots.Where(s => s.Slot != GearSlot.Trinket).ToList() ?? [];

    protected override async Task OnParametersSetAsync()
    {
        info = Site.Role.FromSlug(Slug);
        if (info is null)
        {
            Meta = null;
        }
        // Navigation d'une spécialisation à l'autre : le composant est réutilisé, seul Slug change.
        else if (Meta?.Spec != info.Spec)
        {
            Meta = await Queries.GetLatestAsync(info.Spec);
            refreshTooltips = true;
        }

        players = Meta?.Players.ToList() ?? [];
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender || refreshTooltips)
        {
            refreshTooltips = false;
            await JS.InvokeVoidAsync("pulse.refreshTooltips");
        }
    }
}
