using AnomalyDetection.Core.Models;

namespace AnomalyDetection.Core.Services;

public interface IFoundryService
{
    Task<FoundryResponse> AnalyzeAsync(EvidenceBundle evidence, CancellationToken cancellationToken);
}
