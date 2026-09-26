namespace AnomalyDetection.Core.Services;

public interface IKibanaBootstrapService
{
    Task BootstrapAsync(CancellationToken cancellationToken);
}
