using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Domain;
using Noovio.FrontPulse.Domain.Entities;
using Noovio.FrontPulse.Web.Auth;

namespace Noovio.FrontPulse.Web.Components.Pages;

public partial class Account
{
    /// <summary>Niveau maximum de l'extension (Midnight) : en dessous, la comparaison au top n'a pas de sens.</summary>
    private const int MaxLevel = 90;

    [Inject] private IAccountStore Accounts { get; set; } = default!;

    [CascadingParameter] private Task<AuthenticationState> AuthenticationState { get; set; } = default!;

    /// <summary>Spécialisation d'où vient la demande de comparaison (bouton « Comparer ») : ses personnages passent en tête.</summary>
    [SupplyParameterFromQuery(Name = "spec")] private string? SpecSlug { get; set; }

    private TankSpecInfo? target;

    private UserAccount? account;
    private List<UserCharacter> characters = [];
    private List<UserCharacter> tanks = [];
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
        target = TankSpecs.FromSlug(SpecSlug);
        tanks = ordered.Where(c => TankSpecs.ForClass(c.ClassId) is not null)
            .OrderByDescending(IsTarget) // tri stable : l'ordre par niveau est conservé dans chaque groupe
            .ToList();
        others = ordered.Where(c => TankSpecs.ForClass(c.ClassId) is null).ToList();
    }

    private bool IsTarget(UserCharacter character) => target is not null && character.ClassId == target.ClassId;
}
