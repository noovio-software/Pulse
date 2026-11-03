using Noovio.FrontPulse.Domain.Entities;
using Noovio.FrontPulse.Domain.Enums;

namespace Noovio.FrontPulse.Application.Ingestion;

/// <summary>Une requête (région, donjon) du classement parcourue, sur le total de la saison.</summary>
public readonly record struct LeaderboardStep(int QueriesDone, int Queries);

/// <summary>
/// Avancement d'une ingestion en cours, tenu en mémoire par <see cref="TankMetaIngestionService"/>
/// (les profils sont lus en parallèle, d'où les compteurs atomiques) et recopié en base par le worker.
/// Le classement en rend compte lui-même, requête par requête, via <see cref="IProgress{T}"/>.
/// </summary>
public sealed class IngestionProgress : IProgress<LeaderboardStep>
{
    private int profilesRead;
    private int specsDone;

    public IngestionPhase Phase { get; private set; } = IngestionPhase.Leaderboard;
    public int LeaderboardQueriesDone { get; private set; }
    public int LeaderboardQueries { get; private set; }
    public DateTimeOffset? ProfilesStartedAtUtc { get; private set; }
    public TankSpec? CurrentSpec { get; private set; }
    public int Candidates { get; private set; }
    public int ProfilesRead => Volatile.Read(ref profilesRead);
    public int SpecsDone => Volatile.Read(ref specsDone);

    public void Report(LeaderboardStep value) => (LeaderboardQueriesDone, LeaderboardQueries) = (value.QueriesDone, value.Queries);

    internal void LeaderboardRead(int candidates, DateTimeOffset at)
    {
        Candidates = candidates;
        ProfilesStartedAtUtc = at;
        Phase = IngestionPhase.Profiles;
    }

    internal void SpecStarted(TankSpec spec) => CurrentSpec = spec;

    internal void ProfileRead() => Interlocked.Increment(ref profilesRead);

    internal void SpecDone() => Interlocked.Increment(ref specsDone);
}
