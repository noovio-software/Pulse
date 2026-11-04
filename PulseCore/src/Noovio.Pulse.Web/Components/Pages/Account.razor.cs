using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Web.Auth;

namespace Noovio.Pulse.Web.Components.Pages;

public partial class Account
{
    /// <summary>Niveau maximum de l'extension (Midnight) : en dessous, la comparaison au top n'a pas de sens.</summary>
    private const int MaxLevel = 90;

    [Inject] private IAccountStore Accounts { get; set; } = default!;

    [CascadingParameter] private Task<AuthenticationState> AuthenticationState { get; set; } = default!;

    /// <summary>Spécialisation d'où vient la demande de comparaison (bouton « Comparer ») : ses personnages passent en tête.</summary>
    [SupplyParameterFromQuery(Name = "spec")] private string? SpecSlug { get; set; }

    private SpecInfo? target;

    private UserAccount? account;
    private List<UserCharacter> characters = [];
    private List<UserCharacter> eligible = [];
    private List<UserCharacter> others = [];

    protected override async Task OnInitializedAsync()
    {
        var user = (await AuthenticationState).User;
        if (user.BattleNetId() is not { } id)
        {
            return;
        }

        account = await Accounts.GetAsync(id, CancellationToken.None);
        characters = account?.Characters ?? [];

        // Niveau maximum d'abord, puis par niveau et par nom.
        var ordered = characters.OrderByDescending(c => c.Level).ThenBy(c => c.Name, StringComparer.CurrentCulture).ToList();
        target = Site.Role.FromSlug(SpecSlug);
        eligible = ordered.Where(c => Site.Role.ForClass(c.ClassId).Count > 0)
            .OrderByDescending(IsTarget) // tri stable : l'ordre par niveau est conservé dans chaque groupe
            .ToList();
        others = ordered.Where(c => Site.Role.ForClass(c.ClassId).Count == 0).ToList();
    }

    /// <summary>Les spécialisations du rôle pour ce personnage, celle d'où vient la demande en premier.</summary>
    private List<SpecInfo> SpecsOf(UserCharacter character) =>
        Site.Role.ForClass(character.ClassId).OrderByDescending(s => s == target).ToList();

    private bool IsTarget(UserCharacter character) => target is not null && character.ClassId == target.ClassId;
}
