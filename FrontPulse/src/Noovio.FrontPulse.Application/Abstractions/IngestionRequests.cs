using Noovio.FrontPulse.Domain.Entities;

namespace Noovio.FrontPulse.Application.Abstractions;

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
