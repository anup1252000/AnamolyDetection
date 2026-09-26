using AnomalyDetection.Core.Configuration;
using AnomalyDetection.Core.Services;
using AnomalyDetection.Infrastructure.Elasticsearch;
using AnomalyDetection.Infrastructure.Foundry;
using AnomalyDetection.Infrastructure.Kibana;
using AnomalyDetection.Infrastructure.Processing;
using AnomalyDetection.Infrastructure.RabbitMq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AnomalyDetection.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAnomalyDetection(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ElasticsearchOptions>(configuration.GetSection(ElasticsearchOptions.SectionName));
        services.Configure<KibanaOptions>(configuration.GetSection(KibanaOptions.SectionName));
        services.Configure<SourceFieldsOptions>(configuration.GetSection(SourceFieldsOptions.SectionName));
        services.Configure<AnomalyDetectionOptions>(configuration.GetSection(AnomalyDetectionOptions.SectionName));
        services.Configure<FoundryOptions>(configuration.GetSection(FoundryOptions.SectionName));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        services.AddHttpClient<IElasticsearchEvidenceService, ElasticsearchEvidenceService>();
        services.AddHttpClient<IRcaPersistenceService, RcaPersistenceService>();
        services.AddHttpClient<IFoundryService, FoundryService>();
        services.AddHttpClient(nameof(KibanaBootstrapService) + "-kibana");
        services.AddHttpClient(nameof(KibanaBootstrapService) + "-elasticsearch");

        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<IAnomalyPublisher, AnomalyPublisher>();
        services.AddSingleton<IAnomalyInvestigationProcessor, AnomalyInvestigationProcessor>();
        services.AddSingleton<IKibanaBootstrapService, KibanaBootstrapService>();

        return services;
    }
}
