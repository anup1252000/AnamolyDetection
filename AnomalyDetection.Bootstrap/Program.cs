using AnomalyDetection.Core.Services;
using AnomalyDetection.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddAnomalyDetection(builder.Configuration);

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Bootstrap");

try
{
    var bootstrapService = host.Services.GetRequiredService<IKibanaBootstrapService>();
    await bootstrapService.BootstrapAsync(CancellationToken.None);
    logger.LogInformation("Kibana bootstrap completed successfully.");
}
catch (Exception exception)
{
    logger.LogError(exception, "Kibana bootstrap failed.");
    throw;
}

Console.ReadLine();
