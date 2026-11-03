using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Domain.Entities;

namespace Noovio.FrontPulse.Application.Accounts;

/// <summary>Connexion et synchronisation : crée ou met à jour le compte, puis remplace la liste de ses personnages.</summary>
public sealed class AccountSyncService(IAccountStore accounts, IAccountCharactersSource source, TimeProvider time)
{
    public async Task<UserAccount> SignInAsync(long battleNetId, string battleTag, string userAccessToken, CancellationToken ct)
    {
        var account = await accounts.UpsertAsync(battleNetId, battleTag, ct);
        await SyncAsync(account, userAccessToken, ct);
        return account;
    }

    public async Task SyncAsync(long battleNetId, string userAccessToken, CancellationToken ct)
    {
        if (await accounts.GetAsync(battleNetId, ct) is { } account)
        {
            await SyncAsync(account, userAccessToken, ct);
        }
    }

    private async Task SyncAsync(UserAccount account, string userAccessToken, CancellationToken ct)
    {
        var characters = await source.GetCharactersAsync(userAccessToken, ct);
        var now = time.GetUtcNow();
        foreach (var c in characters)
        {
            c.AccountId = account.Id;
            c.SyncedAtUtc = now;
        }

        await accounts.ReplaceCharactersAsync(account.Id, characters, ct);
    }
}
