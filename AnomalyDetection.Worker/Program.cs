using AnomalyDetection.Infrastructure.DependencyInjection;
using AnomalyDetection.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddAnomalyDetection(builder.Configuration);
builder.Services.AddHostedService<RcaWorker>();

var host = builder.Build();
host.Run();
