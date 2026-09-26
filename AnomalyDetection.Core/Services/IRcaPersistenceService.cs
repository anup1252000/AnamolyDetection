using AnomalyDetection.Core.Models;

namespace AnomalyDetection.Core.Services;

public interface IRcaPersistenceService
{
    Task<bool> ExistsAsync(string id, CancellationToken cancellationToken);
    Task EnsureIndexAsync(CancellationToken cancellationToken);
    Task PersistAsync(RcaRecord record, CancellationToken cancellationToken);
}
