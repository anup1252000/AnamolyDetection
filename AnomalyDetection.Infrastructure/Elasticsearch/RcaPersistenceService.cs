using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AnomalyDetection.Core.Configuration;
using AnomalyDetection.Core.Models;
using AnomalyDetection.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AnomalyDetection.Infrastructure.Elasticsearch;

public sealed class RcaPersistenceService : IRcaPersistenceService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ElasticsearchOptions _options;
    private readonly ILogger<RcaPersistenceService> _logger;

    public RcaPersistenceService(HttpClient httpClient, IOptions<ElasticsearchOptions> options, ILogger<RcaPersistenceService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        ConfigureClient(_httpClient, _options);
    }

    public async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.AnomalyResultsIndex}/_search")
        {
            Content = new StringContent(new JsonObject
            {
                ["size"] = 1,
                ["_source"] = false,
                ["query"] = new JsonObject
                {
                    ["ids"] = new JsonObject
                    {
                        ["values"] = new JsonArray(id)
                    }
                }
            }.ToJsonString(), Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);
        return document.RootElement.GetProperty("hits").GetProperty("hits").GetArrayLength() > 0;
    }

    public async Task EnsureIndexAsync(CancellationToken cancellationToken)
    {
        var indexName = GetConcreteIndexName(DateTimeOffset.UtcNow);
        using var existsResponse = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, indexName), cancellationToken);
        if (existsResponse.StatusCode == HttpStatusCode.OK)
        {
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Put, indexName)
        {
            Content = new StringContent(BuildMapping().ToJsonString(), Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
        _logger.LogInformation("Ensured anomaly results index {IndexName}", indexName);
    }

    public async Task PersistAsync(RcaRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        var indexName = GetConcreteIndexName(record.Timestamp);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{indexName}/_doc/{Uri.EscapeDataString(record.Id)}")
        {
            Content = new StringContent(JsonSerializer.Serialize(record, SerializerOptions), Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private JsonObject BuildMapping()
    {
        return new JsonObject
        {
            ["mappings"] = new JsonObject
            {
                ["dynamic"] = false,
                ["properties"] = new JsonObject
                {
                    ["@timestamp"] = new JsonObject { ["type"] = "date" },
                    ["jobId"] = new JsonObject { ["type"] = "keyword" },
                    ["detector"] = new JsonObject { ["type"] = "keyword" },
                    ["anomalyScore"] = new JsonObject { ["type"] = "double" },
                    ["service"] = new JsonObject { ["type"] = "keyword" },
                    ["environment"] = new JsonObject { ["type"] = "keyword" },
                    ["severity"] = new JsonObject { ["type"] = "keyword" },
                    ["rootCause"] = new JsonObject { ["type"] = "text" },
                    ["confidence"] = new JsonObject { ["type"] = "double" },
                    ["explanation"] = new JsonObject { ["type"] = "text" },
                    ["evidence"] = new JsonObject { ["type"] = "text" },
                    ["recommendations"] = new JsonObject { ["type"] = "text" },
                    ["affectedServices"] = new JsonObject { ["type"] = "keyword" },
                    ["influencer"] = new JsonObject { ["type"] = "keyword" },
                    ["resultType"] = new JsonObject { ["type"] = "keyword" },
                    ["actual"] = new JsonObject { ["type"] = "double" },
                    ["typical"] = new JsonObject { ["type"] = "double" }
                }
            }
        };
    }

    private string GetConcreteIndexName(DateTimeOffset timestamp)
    {
        var pattern = _options.AnomalyResultsIndex.Trim();
        if (!pattern.Contains('*', StringComparison.Ordinal))
        {
            return pattern;
        }

        return pattern.Replace("*", timestamp.UtcDateTime.ToString("yyyy.MM.dd"), StringComparison.Ordinal);
    }

    private static void ConfigureClient(HttpClient httpClient, ElasticsearchOptions options)
    {
        if (httpClient.BaseAddress is null)
        {
            httpClient.BaseAddress = new Uri(options.Url, UriKind.Absolute);
        }

        if (httpClient.DefaultRequestHeaders.Accept.Count == 0)
        {
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", options.ApiKey);
            return;
        }

        if (!string.IsNullOrWhiteSpace(options.Username) && !string.IsNullOrWhiteSpace(options.Password))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Username}:{options.Password}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }
    }
}
