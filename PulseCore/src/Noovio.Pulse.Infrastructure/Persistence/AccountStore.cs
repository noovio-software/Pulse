using Microsoft.EntityFrameworkCore;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Domain.Entities;

namespace Noovio.Pulse.Infrastructure.Persistence;

public sealed class AccountStore(PulseDbContext db, TimeProvider time) : IAccountStore
{
    public async Task<UserAccount> UpsertAsync(long battleNetId, string battleTag, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var account = await db.UserAccounts.FirstOrDefaultAsync(a => a.BattleNetId == battleNetId, ct);
        if (account is null)
        {
            account = new UserAccount { BattleNetId = battleNetId, CreatedAtUtc = now };
            db.UserAccounts.Add(account);
        }

        account.BattleTag = battleTag;
        account.LastLoginAtUtc = now;
        await db.SaveChangesAsync(ct);
        return account;
    }

    public async Task ReplaceCharactersAsync(long accountId, IReadOnlyList<UserCharacter> characters, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.UserCharacters.Where(c => c.AccountId == accountId).ExecuteDeleteAsync(ct);
        db.UserCharacters.AddRange(characters);
        await db.UserAccounts.Where(a => a.Id == accountId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.LastSyncAtUtc, time.GetUtcNow()), ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    public Task<UserAccount?> GetAsync(long battleNetId, CancellationToken ct) =>
        db.UserAccounts.AsNoTracking()
            .Include(a => a.Characters)
            .FirstOrDefaultAsync(a => a.BattleNetId == battleNetId, ct);

    public Task<UserCharacter?> GetCharacterAsync(long battleNetId, long characterId, CancellationToken ct) =>
        db.UserCharacters.AsNoTracking()
            .Where(c => c.Id == characterId && db.UserAccounts.Any(a => a.Id == c.AccountId && a.BattleNetId == battleNetId))
            .FirstOrDefaultAsync(ct);
}
