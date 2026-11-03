namespace Noovio.FrontPulse.Domain.Entities;

public enum IngestionRequestStatus
{
    Pending,
    Running,
    Succeeded,
    Failed
}

/// <summary>
/// Demande d'ingestion hors calendrier, déposée par le site (page d'admin ou API) et exécutée par le worker,
/// qui la relève à son prochain passage (5 s au plus). Une demande déposée pendant une exécution
/// la rejoint : elle passe « en cours » aussitôt et se termine avec elle.
/// </summary>
public sealed class IngestionRequest
{
    public long Id { get; set; }
    public DateTimeOffset RequestedAtUtc { get; set; }

    /// <summary>BattleTag de l'admin, ou « api » pour un appel avec la clé d'admin.</summary>
    public string RequestedBy { get; set; } = "";

    public IngestionRequestStatus Status { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }

    /// <summary>Résumé de l'exécution (tanks par spécialisation) ou message d'erreur.</summary>
    public string? Message { get; set; }
}
