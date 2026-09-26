using AnomalyDetection.Core.Models;

namespace AnomalyDetection.Core.Services;

public interface IAnomalyInvestigationProcessor
{
    Task ProcessAsync(AnomalyMessage message, CancellationToken cancellationToken);
}
