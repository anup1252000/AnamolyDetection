using AnomalyDetection.Core.Models;

namespace AnomalyDetection.Core.Services;

public interface IElasticsearchEvidenceService
{
    Task<EvidenceBundle> GetEvidenceAsync(AnomalyMessage anomaly, CancellationToken cancellationToken);
}
