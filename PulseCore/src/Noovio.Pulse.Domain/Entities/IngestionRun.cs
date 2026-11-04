using Noovio.Pulse.Domain.Enums;

namespace Noovio.Pulse.Domain.Entities;

public enum IngestionRunStatus
{
    Running,
    Succeeded,
    Failed
}

/// <summary>Classement Raider.io d'abord (toutes spécialisations), puis profils Blizzard et agrégation, spécialisation par spécialisation.</summary>
public enum IngestionPhase
{
    Leaderboard,
    Profiles
}

/// <summary>
/// Une exécution de l'ingestion, automatique ou forcée, avec son avancement : le worker la met à jour
/// pendant qu'il tourne, la page d'admin en tire une barre de progression et une durée estimée.
/// </summary>
public sealed class IngestionRun
{
    public long Id { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }

    /// <summary>Dernier compte rendu du worker : s'il date, le worker est arrêté ou bloqué.</summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    /// <summary>Au moins une demande forcée (page d'admin ou API) est servie par cette exécution.</summary>
    public bool Forced { get; set; }

    public IngestionRunStatus Status { get; set; }
    public IngestionPhase Phase { get; set; }

    /// <summary>Requêtes (région, donjon) du classement déjà parcourues, sur le total.</summary>
    public int LeaderboardQueriesDone { get; set; }
    public int LeaderboardQueries { get; set; }

    /// <summary>Fin de la lecture du classement : le rythme de lecture des profils se mesure à partir d'ici.</summary>
    public DateTimeOffset? ProfilesStartedAtUtc { get; set; }

    public WowSpec? CurrentSpec { get; set; }
    public int SpecsDone { get; set; }

    /// <summary>Profils à lire, toutes spécialisations confondues (les candidats du classement).</summary>
    public int Candidates { get; set; }

    public int ProfilesRead { get; set; }

    /// <summary>Résumé de l'exécution (joueurs par spécialisation) ou message d'erreur.</summary>
    public string? Message { get; set; }
}
