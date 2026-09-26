using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using AnomalyDetection.Core.Configuration;
using AnomalyDetection.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AnomalyDetection.Infrastructure.Kibana;

public sealed class KibanaBootstrapService : IKibanaBootstrapService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly KibanaOptions _kibanaOptions;
    private readonly ElasticsearchOptions _elasticsearchOptions;
    private readonly AnomalyDetectionOptions _anomalyDetectionOptions;
    private readonly SourceFieldsOptions _sourceFieldsOptions;
    private readonly ILogger<KibanaBootstrapService> _logger;

    public KibanaBootstrapService(
        IHttpClientFactory httpClientFactory,
        IOptions<KibanaOptions> kibanaOptions,
        IOptions<ElasticsearchOptions> elasticsearchOptions,
        IOptions<AnomalyDetectionOptions> anomalyDetectionOptions,
        IOptions<SourceFieldsOptions> sourceFieldsOptions,
        ILogger<KibanaBootstrapService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _kibanaOptions = kibanaOptions.Value;
        _elasticsearchOptions = elasticsearchOptions.Value;
        _anomalyDetectionOptions = anomalyDetectionOptions.Value;
        _sourceFieldsOptions = sourceFieldsOptions.Value;
        _logger = logger;
    }

    public async Task BootstrapAsync(CancellationToken cancellationToken)
    {
        var sourceDataViewId = _kibanaOptions.DataViewId ?? Slugify(_kibanaOptions.DataViewName);
        var resultsDataViewId = _kibanaOptions.ResultsDataViewId ?? Slugify(_kibanaOptions.ResultsDataViewName);

        await EnsureDataViewAsync(sourceDataViewId, _kibanaOptions.DataViewName, _elasticsearchOptions.SourceIndex, cancellationToken);
        await EnsureDataViewAsync(resultsDataViewId, _kibanaOptions.ResultsDataViewName, _elasticsearchOptions.AnomalyResultsIndex, cancellationToken);
        await EnsureMlJobAsync(cancellationToken);
        await EnsureDatafeedAsync(cancellationToken);
        await EnsureDashboardAsync(resultsDataViewId, cancellationToken);
    }

    private async Task EnsureDataViewAsync(string dataViewId, string title, string indexPattern, CancellationToken cancellationToken)
    {
        var kibanaClient = CreateKibanaClient();
        var path = $"{GetKibanaPrefix()}/api/data_views/data_view/{Uri.EscapeDataString(dataViewId)}";
        using var getResponse = await kibanaClient.GetAsync(path, cancellationToken);
        if (getResponse.StatusCode == HttpStatusCode.OK)
        {
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{GetKibanaPrefix()}/api/data_views/data_view")
        {
            Content = new StringContent(
                new JsonObject
                {
                    ["data_view"] = new JsonObject
                    {
                        ["id"] = dataViewId,
                        ["name"] = title,
                        ["title"] = indexPattern,
                        ["timeFieldName"] = _sourceFieldsOptions.Timestamp
                    }
                }.ToJsonString(),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("kbn-xsrf", "true");

        using var response = await kibanaClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return;
        }

        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Ensured Kibana data view {DataViewName}", title);
    }

    private async Task EnsureMlJobAsync(CancellationToken cancellationToken)
    {
        var elasticClient = CreateElasticsearchClient();
        var jobPath = $"/_ml/anomaly_detectors/{Uri.EscapeDataString(_anomalyDetectionOptions.JobId)}";
        using var existsResponse = await elasticClient.GetAsync(jobPath, cancellationToken);
        if (existsResponse.StatusCode == HttpStatusCode.OK)
        {
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Put, jobPath)
        {
            Content = new StringContent(BuildMlJobDefinition().ToJsonString(), Encoding.UTF8, "application/json")
        };

        using var response = await elasticClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return;
        }
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Failed to create ML job {JobId}. Status: {StatusCode}. Response: {Response}",
                _anomalyDetectionOptions.JobId,
                (int)response.StatusCode,
                responseBody);

            if (responseBody.Contains("current license is non-compliant for [ml]", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError(
                    "Elasticsearch ML anomaly detection is unavailable with the current license. Use an Elasticsearch license/trial that supports ML or use the configured alternative.");
            }

            throw new HttpRequestException(
                $"Failed to create ML job. HTTP {(int)response.StatusCode}: {responseBody}");
        }

        response.EnsureSuccessStatusCode();
        _logger.LogInformation("Ensured ML job {JobId}", _anomalyDetectionOptions.JobId);
    }

    private async Task EnsureDatafeedAsync(CancellationToken cancellationToken)
    {
        var elasticClient = CreateElasticsearchClient();
        var datafeedPath = $"/_ml/datafeeds/{Uri.EscapeDataString(_anomalyDetectionOptions.DatafeedId)}";
        using var existsResponse = await elasticClient.GetAsync(datafeedPath, cancellationToken);
        if (existsResponse.StatusCode == HttpStatusCode.OK)
        {
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Put, datafeedPath)
        {
            Content = new StringContent(BuildDatafeedDefinition().ToJsonString(), Encoding.UTF8, "application/json")
        };

        using var response = await elasticClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
        _logger.LogInformation("Ensured ML datafeed {DatafeedId}", _anomalyDetectionOptions.DatafeedId);
    }

    private JsonObject BuildMlJobDefinition()
    {
        var detectors = new JsonArray();

        foreach (var detector in _anomalyDetectionOptions.Detectors)
        {
            var detectorJson = new JsonObject
            {
                ["detector_description"] = detector.Description,
                ["function"] = detector.Function
            };

            if (!string.IsNullOrWhiteSpace(detector.FieldName))
            {
                detectorJson["field_name"] = detector.FieldName;
            }

            if (!string.IsNullOrWhiteSpace(detector.ByFieldName))
            {
                detectorJson["by_field_name"] = detector.ByFieldName;
            }

            if (!string.IsNullOrWhiteSpace(detector.OverFieldName))
            {
                detectorJson["over_field_name"] = detector.OverFieldName;
            }

            if (!string.IsNullOrWhiteSpace(detector.PartitionFieldName))
            {
                detectorJson["partition_field_name"] =
                    detector.PartitionFieldName;
            }

            detectors.Add(detectorJson);
        }

        var influencers = new JsonArray();

        foreach (var influencer in _anomalyDetectionOptions.Influencers)
        {
            if (!string.IsNullOrWhiteSpace(influencer))
            {
                influencers.Add(influencer);
            }
        }

        return new JsonObject
        {
            ["description"] =
                "Anomaly detection for application telemetry",

            ["analysis_config"] = new JsonObject
            {
                ["bucket_span"] =
                    _anomalyDetectionOptions.BucketSpan,

                ["detectors"] = detectors,

                ["influencers"] = influencers
            },

            ["data_description"] = new JsonObject
            {
                ["time_field"] =
                    _sourceFieldsOptions.Timestamp
            }
        };
    }

    //private JsonObject BuildMlJobDefinition()
    //{
    //    var detectors = new JsonArray();
    //    foreach (var detector in _anomalyDetectionOptions.Detectors)
    //    {
    //        //detectors.Add(new JsonObject
    //        //{
    //        //    ["detector_description"] = detector.Description,
    //        //    ["function"] = detector.Function,
    //        //    ["field_name"] = detector.FieldName,
    //        //    ["by_field_name"] = detector.ByFieldName,
    //        //    ["over_field_name"] = detector.OverFieldName,
    //        //    ["partition_field_name"] = detector.PartitionFieldName
    //        //});

    //        var detectorJson = new JsonObject
    //        {
    //            ["detector_description"] = detector.Description,
    //            ["function"] = detector.Function
    //        };

    //        if (!string.IsNullOrWhiteSpace(detector.FieldName))
    //        {
    //            detectorJson["field_name"] = detector.FieldName;
    //        }

    //        if (!string.IsNullOrWhiteSpace(detector.ByFieldName))
    //        {
    //            detectorJson["by_field_name"] = detector.ByFieldName;
    //        }

    //        if (!string.IsNullOrWhiteSpace(detector.OverFieldName))
    //        {
    //            detectorJson["over_field_name"] = detector.OverFieldName;
    //        }

    //        if (!string.IsNullOrWhiteSpace(detector.PartitionFieldName))
    //        {
    //            detectorJson["partition_field_name"] = detector.PartitionFieldName;
    //        }

    //        detectors.Add(detectorJson);

    //    }

    //    //var influencers = new JsonArray();
    //    //foreach (var influencer in _anomalyDetectionOptions.Influencers)
    //    //{
    //    //    influencers.Add(influencer);
    //    //}

    //    //return new JsonObject
    //    //{
    //    //    ["description"] = "Anomaly detection for application telemetry",
    //    //    ["analysis_config"] = new JsonObject
    //    //    {
    //    //        ["bucket_span"] = _anomalyDetectionOptions.BucketSpan,
    //    //        ["detectors"] = detectors,
    //    //        ["influencers"] = influencers
    //    //    },
    //    //    ["data_description"] = new JsonObject
    //    //    {
    //    //        ["time_field"] = _sourceFieldsOptions.Timestamp
    //    //    }
    //    //};

    //    var influencers = new JsonArray();

    //    foreach (var influencer in _anomalyDetectionOptions.Influencers)
    //    {
    //        if (!string.IsNullOrWhiteSpace(influencer))
    //        {
    //            influencers.Add(influencer);
    //        }
    //    }

    //    return new JsonObject
    //    {
    //        ["description"] = "Anomaly detection for application telemetry",

    //        ["analysis_config"] = new JsonObject
    //        {
    //            ["bucket_span"] = _anomalyDetectionOptions.BucketSpan,
    //            ["detectors"] = detectors,
    //            ["influencers"] = influencers
    //        },

    //        ["data_description"] = new JsonObject
    //        {
    //            ["time_field"] = _sourceFieldsOptions.Timestamp
    //        }
    //    };
    //}

    private JsonObject BuildDatafeedDefinition()
    {
        return new JsonObject
        {
            ["job_id"] = _anomalyDetectionOptions.JobId,
            ["indices"] = new JsonArray(_elasticsearchOptions.SourceIndex),
            ["query"] = new JsonObject
            {
                ["match_all"] = new JsonObject()
            }
        };
    }

    private async Task EnsureDashboardAsync(string resultsDataViewId, CancellationToken cancellationToken)
    {
        var dashboardId = _kibanaOptions.DashboardId ?? Slugify(_kibanaOptions.DashboardTitle);
        var kibanaClient = CreateKibanaClient();
        var path = $"{GetKibanaPrefix()}/api/saved_objects/dashboard/{Uri.EscapeDataString(dashboardId)}";
        using var getResponse = await kibanaClient.GetAsync(path, cancellationToken);
        if (getResponse.StatusCode == HttpStatusCode.OK)
        {
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{GetKibanaPrefix()}/api/saved_objects/dashboard/{Uri.EscapeDataString(dashboardId)}?overwrite=true")
        {
            Content = new StringContent(BuildDashboardDefinition().ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("kbn-xsrf", "true");

        using var response = await kibanaClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        _logger.LogInformation("Ensured Kibana dashboard {DashboardTitle} using data view {DataViewId}", _kibanaOptions.DashboardTitle, resultsDataViewId);
    }

    private JsonObject BuildDashboardDefinition()
    {
        return new JsonObject
        {
            ["attributes"] = new JsonObject
            {
                ["title"] = _kibanaOptions.DashboardTitle,
                ["description"] = "Application anomaly RCA dashboard for ai-anomalies-* results.",
                ["hits"] = 0,
                ["timeRestore"] = false,
                ["optionsJSON"] = "{\"useMargins\":true,\"syncColors\":false,\"hidePanelTitles\":false}",
                ["panelsJSON"] = "[]",
                ["kibanaSavedObjectMeta"] = new JsonObject
                {
                    ["searchSourceJSON"] = "{\"query\":{\"language\":\"kuery\",\"query\":\"\"},\"filter\":[]}"
                }
            }
        };
    }

    private HttpClient CreateKibanaClient()
    {
        var client = _httpClientFactory.CreateClient(nameof(KibanaBootstrapService) + "-kibana");
        if (client.BaseAddress is null)
        {
            client.BaseAddress = new Uri(_kibanaOptions.Url, UriKind.Absolute);
        }

        if (client.DefaultRequestHeaders.Authorization is null && !string.IsNullOrWhiteSpace(_kibanaOptions.ApiKey))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", _kibanaOptions.ApiKey);
        }

        return client;
    }

    private HttpClient CreateElasticsearchClient()
    {
        var client = _httpClientFactory.CreateClient(nameof(KibanaBootstrapService) + "-elasticsearch");
        if (client.BaseAddress is null)
        {
            client.BaseAddress = new Uri(_elasticsearchOptions.Url, UriKind.Absolute);
        }

        if (client.DefaultRequestHeaders.Authorization is null)
        {
            if (!string.IsNullOrWhiteSpace(_elasticsearchOptions.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", _elasticsearchOptions.ApiKey);
            }
            else if (!string.IsNullOrWhiteSpace(_elasticsearchOptions.Username) && !string.IsNullOrWhiteSpace(_elasticsearchOptions.Password))
            {
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_elasticsearchOptions.Username}:{_elasticsearchOptions.Password}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }
        }

        return client;
    }

    private string GetKibanaPrefix()
    {
        return string.Equals(_kibanaOptions.Space, "default", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : $"/s/{Uri.EscapeDataString(_kibanaOptions.Space)}";
    }

    private static string Slugify(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '-');
        }

        return builder.ToString().Trim('-');
    }
}
