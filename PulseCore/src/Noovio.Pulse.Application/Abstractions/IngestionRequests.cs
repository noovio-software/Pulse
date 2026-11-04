using Noovio.Pulse.Application.Ingestion;
using Noovio.Pulse.Domain.Entities;

namespace Noovio.Pulse.Application.Abstractions;

/// <summary>File des demandes d'ingestion forcée, partagée entre le site (dépôt) et le worker (exécution).</summary>
public interface IIngestionRequestStore
{
    /// <summary>Dépose une demande ; s'il y en a déjà une en attente, la renvoie au lieu d'en créer une seconde.</summary>
    Task<IngestionRequest> RequestAsync(string requestedBy, CancellationToken ct);

    /// <summary>Passe toutes les demandes en attente à « en cours » et les renvoie (vide s'il n'y en a pas).</summary>
    Task<IReadOnlyList<IngestionRequest>> StartPendingAsync(CancellationToken ct);

    Task CompleteAsync(IReadOnlyList<long> ids, bool succeeded, string message, CancellationToken ct);

    Task<IReadOnlyList<IngestionRequest>> GetRecentAsync(int count, CancellationToken ct);

    /// <summary>Au démarrage du worker : les demandes restées « en cours » ont été coupées par un arrêt (déploiement).</summary>
    Task FailInterruptedAsync(CancellationToken ct);
}

/// <summary>Exécutions de l'ingestion et leur avancement : écrites par le worker, lues par la page d'admin.</summary>
public interface IIngestionRunStore
{
    Task<IngestionRun> StartAsync(bool forced, CancellationToken ct);

    /// <summary>Recopie l'avancement en mémoire ; <paramref name="forced"/> passe à vrai quand une demande rejoint l'exécution.</summary>
    Task ReportAsync(long id, IngestionProgress progress, bool forced, CancellationToken ct);

    Task CompleteAsync(long id, bool succeeded, string message, CancellationToken ct);

    /// <summary>Plus récentes d'abord.</summary>
    Task<IReadOnlyList<IngestionRun>> GetRecentAsync(int count, CancellationToken ct);

    /// <summary>Au démarrage du worker : les exécutions restées « en cours » ont été coupées par un arrêt (déploiement).</summary>
    Task FailInterruptedAsync(CancellationToken ct);
}
