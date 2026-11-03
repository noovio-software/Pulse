using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Domain;
using Noovio.FrontPulse.Domain.Entities;
using Noovio.FrontPulse.Domain.Enums;

namespace Noovio.FrontPulse.Web.Components.Pages;

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

    private TankSpecInfo? info;
    private bool refreshTooltips;

    /// <summary>Virtualize exige une ICollection ; le snapshot expose une IReadOnlyList.</summary>
    private List<TopTank> players = [];

    /// <summary>Les bijoux ont leur propre section (défensifs / offensifs).</summary>
    private IReadOnlyList<SlotUsage> GearSlots => Meta?.Slots.Where(s => s.Slot != GearSlot.Trinket).ToList() ?? [];

    protected override async Task OnParametersSetAsync()
    {
        info = TankSpecs.FromSlug(Slug);
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
            await JS.InvokeVoidAsync("frontpulse.refreshTooltips");
        }
    }
}
