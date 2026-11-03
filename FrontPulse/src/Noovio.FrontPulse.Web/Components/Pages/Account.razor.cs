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
        tanks = ordered.Where(c => TankSpecs.ForClass(c.ClassId) is not null).ToList();
        others = ordered.Where(c => TankSpecs.ForClass(c.ClassId) is null).ToList();
    }
}
