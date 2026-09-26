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
        services
            .AddOptions<ElasticsearchOptions>()
            .Bind(configuration.GetSection(ElasticsearchOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Url), "Elasticsearch:Url is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.SourceIndex), "Elasticsearch:SourceIndex is required.")
            .ValidateOnStart();

        services
            .AddOptions<KibanaOptions>()
            .Bind(configuration.GetSection(KibanaOptions.SectionName))
            .ValidateOnStart();

        services
            .AddOptions<SourceFieldsOptions>()
            .Bind(configuration.GetSection(SourceFieldsOptions.SectionName))
            .ValidateOnStart();

        services
            .AddOptions<AnomalyDetectionOptions>()
            .Bind(configuration.GetSection(AnomalyDetectionOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.JobId), "AnomalyDetection:JobId is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.DatafeedId), "AnomalyDetection:DatafeedId is required.")
            .ValidateOnStart();

        services
            .AddOptions<FoundryOptions>()
            .Bind(configuration.GetSection(FoundryOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ProjectEndpoint), "Foundry:ProjectEndpoint is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.AgentName), "Foundry:AgentName is required.")
            .ValidateOnStart();

        services
            .AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "RabbitMQ:Host is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Exchange), "RabbitMQ:Exchange is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Queue), "RabbitMQ:Queue is required.")
            .ValidateOnStart();

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
