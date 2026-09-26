using AnomalyDetection.Core.Models;

namespace AnomalyDetection.Core.Services;

public interface IAnomalyPublisher
{
    Task PublishAsync(AnomalyMessage message, CancellationToken cancellationToken);
}
