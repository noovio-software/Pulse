using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Analysis;
using Noovio.Pulse.Domain.Entities;

namespace Noovio.Pulse.Infrastructure.Persistence;

/// <summary>Une analyse partagée : la copie complète de ce qui était affiché, jamais modifiée après création.</summary>
public sealed class SharedAnalysisRecord
{
    public long Id { get; set; }

    /// <summary>Jeton du lien (128 bits aléatoires, base64url) : impossible à deviner, pas d'identifiant séquentiel exposé.</summary>
    public string Token { get; set; } = "";

    /// <summary>Compte qui a partagé ; jamais affiché.</summary>
    public long BattleNetId { get; set; }

    public long CharacterId { get; set; }
    public string SpecSlug { get; set; } = "";
    public string RealmName { get; set; } = "";
    public DateTimeOffset AnalyzedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public CharacterAnalysis Analysis { get; set; } = default!;
}

public sealed class SharedAnalysisStore(PulseDbContext db, TimeProvider time) : ISharedAnalysisStore
{
    public async Task<string> ShareAsync(long battleNetId, UserCharacter character, CharacterAnalysis analysis, CancellationToken ct)
    {
        // PostgreSQL garde la microseconde, DateTimeOffset le dixième : comparer à la valeur telle qu'elle a été stockée.
        var analyzedAt = analysis.AnalyzedAtUtc.AddTicks(-(analysis.AnalyzedAtUtc.Ticks % 10));
        var existing = await db.SharedAnalyses.AsNoTracking()
            .Where(s => s.BattleNetId == battleNetId && s.CharacterId == character.Id
                        && s.SpecSlug == analysis.Spec.Slug && s.AnalyzedAtUtc == analyzedAt)
            .Select(s => s.Token)
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            return existing;
        }

        var record = new SharedAnalysisRecord
        {
            Token = NewToken(),
            BattleNetId = battleNetId,
            CharacterId = character.Id,
            SpecSlug = analysis.Spec.Slug,
            RealmName = character.RealmName,
            AnalyzedAtUtc = analyzedAt,
            CreatedAtUtc = time.GetUtcNow(),
            Analysis = analysis
        };
        db.SharedAnalyses.Add(record);
        await db.SaveChangesAsync(ct);
        db.Entry(record).State = EntityState.Detached;
        return record.Token;
    }

    public async Task<SharedAnalysis?> GetAsync(string token, CancellationToken ct)
    {
        if (token.Length != TokenLength)
        {
            return null;
        }

        var record = await db.SharedAnalyses.AsNoTracking().FirstOrDefaultAsync(s => s.Token == token, ct);
        return record is null ? null : new SharedAnalysis(record.Token, record.RealmName, record.Analysis, record.CreatedAtUtc);
    }

    internal const int TokenLength = 22;

    internal static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
