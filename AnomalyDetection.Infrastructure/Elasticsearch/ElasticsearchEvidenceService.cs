using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AnomalyDetection.Core.Configuration;
using AnomalyDetection.Core.Models;
using AnomalyDetection.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AnomalyDetection.Infrastructure.Elasticsearch;

public sealed class ElasticsearchEvidenceService : IElasticsearchEvidenceService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<ElasticsearchEvidenceService> _logger;
    private readonly ElasticsearchOptions _elasticsearchOptions;
    private readonly SourceFieldsOptions _sourceFields;
    private readonly ElasticsearchEvidenceQueryBuilder _queryBuilder;

    public ElasticsearchEvidenceService(
        HttpClient httpClient,
        IOptions<ElasticsearchOptions> elasticsearchOptions,
        IOptions<SourceFieldsOptions> sourceFields,
        ILogger<ElasticsearchEvidenceService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _elasticsearchOptions = elasticsearchOptions.Value;
        _sourceFields = sourceFields.Value;
        _queryBuilder = new ElasticsearchEvidenceQueryBuilder(_elasticsearchOptions, _sourceFields);

        ConfigureClient(_httpClient, _elasticsearchOptions);
    }

    public async Task<EvidenceBundle> GetEvidenceAsync(AnomalyMessage anomaly, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(anomaly);

        var requestUri = BuildSearchUri();
        var payload = _queryBuilder.Build(anomaly);
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
        };

        _logger.LogInformation(
            "Retrieving evidence for anomaly {AnomalyId} in service {Service}",
            anomaly.Id,
            anomaly.Service);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);

        var evidence = document.RootElement
            .GetProperty("hits")
            .GetProperty("hits")
            .EnumerateArray()
            .Select(MapEvidence)
            .ToArray();

        _logger.LogInformation(
            "Retrieved {EvidenceCount} evidence document(s) for anomaly {AnomalyId}",
            evidence.Length,
            anomaly.Id);

        return new EvidenceBundle
        {
            Anomaly = anomaly,
            Documents = evidence
        };
    }

    private string BuildSearchUri()
    {
        return $"{_elasticsearchOptions.SourceIndex}/_search";
    }

    private EvidenceDocument MapEvidence(JsonElement hit)
    {
        if (!hit.TryGetProperty("_source", out var source))
        {
            return new EvidenceDocument();
        }

        var additionalFields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in _sourceFields.AdditionalFields.Where(static field => !string.IsNullOrWhiteSpace(field)))
        {
            additionalFields[field] = ReadStringValue(source, field);
        }

        return new EvidenceDocument
        {
            Timestamp = ReadDateTimeOffset(source, _sourceFields.Timestamp),
            Service = ReadStringValue(source, _sourceFields.Service),
            Environment = ReadStringValue(source, _sourceFields.Environment),
            Route = ReadStringValue(source, _sourceFields.Route),
            Duration = ReadDoubleValue(source, _sourceFields.Duration),
            StatusCode = ReadIntValue(source, _sourceFields.StatusCode),
            Message = ReadStringValue(source, _sourceFields.Message),
            Exception = ReadStringValue(source, _sourceFields.Exception),
            TraceId = ReadStringValue(source, _sourceFields.TraceId),
            AdditionalFields = additionalFields
        };
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

    private static JsonElement? GetElement(JsonElement source, string fieldPath)
    {
        var current = source;
        foreach (var segment in fieldPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    private static string? ReadStringValue(JsonElement source, string fieldPath)
    {
        var element = GetElement(source, fieldPath);
        if (element is null)
        {
            return null;
        }

        return element.Value.ValueKind switch
        {
            JsonValueKind.String => element.Value.GetString(),
            JsonValueKind.Number => element.Value.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Array => element.Value.ToString(),
            JsonValueKind.Object => element.Value.ToString(),
            _ => null
        };
    }

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement source, string fieldPath)
    {
        var value = ReadStringValue(source, fieldPath);
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    private static double? ReadDoubleValue(JsonElement source, string fieldPath)
    {
        var element = GetElement(source, fieldPath);
        if (element is null)
        {
            return null;
        }

        if (element.Value.ValueKind == JsonValueKind.Number && element.Value.TryGetDouble(out var number))
        {
            return number;
        }

        return double.TryParse(ReadStringValue(source, fieldPath), out var parsed) ? parsed : null;
    }

    private static int? ReadIntValue(JsonElement source, string fieldPath)
    {
        var element = GetElement(source, fieldPath);
        if (element is null)
        {
            return null;
        }

        if (element.Value.ValueKind == JsonValueKind.Number && element.Value.TryGetInt32(out var number))
        {
            return number;
        }

        return int.TryParse(ReadStringValue(source, fieldPath), out var parsed) ? parsed : null;
    }
}
