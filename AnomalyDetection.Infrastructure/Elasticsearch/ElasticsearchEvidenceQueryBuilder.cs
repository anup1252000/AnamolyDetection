using System.Text.Json.Nodes;
using AnomalyDetection.Core.Configuration;
using AnomalyDetection.Core.Models;

namespace AnomalyDetection.Infrastructure.Elasticsearch;

public sealed class ElasticsearchEvidenceQueryBuilder
{
    private readonly ElasticsearchOptions _elasticsearchOptions;
    private readonly SourceFieldsOptions _sourceFields;

    public ElasticsearchEvidenceQueryBuilder(ElasticsearchOptions elasticsearchOptions, SourceFieldsOptions sourceFields)
    {
        _elasticsearchOptions = elasticsearchOptions;
        _sourceFields = sourceFields;
    }

    public JsonObject Build(AnomalyMessage anomaly)
    {
        var from = anomaly.Timestamp.AddMinutes(-Math.Abs(_elasticsearchOptions.EvidenceWindowMinutes));
        var to = anomaly.Timestamp.AddMinutes(Math.Abs(_elasticsearchOptions.EvidenceWindowMinutes));

        var must = new JsonArray
        {
            new JsonObject
            {
                ["range"] = new JsonObject
                {
                    [_sourceFields.Timestamp] = new JsonObject
                    {
                        ["gte"] = from,
                        ["lte"] = to,
                        ["format"] = "strict_date_optional_time"
                    }
                }
            }
        };

        AddTermIfPresent(must, _sourceFields.Service, anomaly.Service);
        AddTermIfPresent(must, _sourceFields.Environment, anomaly.Environment);
        AddTermIfPresent(must, _sourceFields.TraceId, anomaly.Influencer);

        var sourceFields = new JsonArray();
        foreach (var field in GetSourceFields())
        {
            sourceFields.Add(field);
        }

        return new JsonObject
        {
            ["size"] = Math.Max(1, _elasticsearchOptions.EvidenceSizeLimit),
            ["sort"] = new JsonArray
            {
                new JsonObject
                {
                    [_sourceFields.Timestamp] = new JsonObject
                    {
                        ["order"] = "desc"
                    }
                }
            },
            ["_source"] = sourceFields,
            ["query"] = new JsonObject
            {
                ["bool"] = new JsonObject
                {
                    ["must"] = must
                }
            }
        };
    }

    private IReadOnlyList<JsonNode?> GetSourceFields()
    {
        var fields = new List<JsonNode?>
        {
            _sourceFields.Timestamp,
            _sourceFields.Service,
            _sourceFields.Environment,
            _sourceFields.Route,
            _sourceFields.Duration,
            _sourceFields.StatusCode,
            _sourceFields.Message,
            _sourceFields.Exception,
            _sourceFields.TraceId
        };

        foreach (var field in _sourceFields.AdditionalFields.Where(static field => !string.IsNullOrWhiteSpace(field)))
        {
            if (!fields.OfType<JsonValue>().Any(existing => string.Equals(existing.ToString(), field, StringComparison.Ordinal)))
            {
                fields.Add(field);
            }
        }

        return fields;
    }

    private static void AddTermIfPresent(JsonArray must, string fieldName, string? value)
    {
        if (string.IsNullOrWhiteSpace(fieldName) || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        must.Add(new JsonObject
        {
            ["term"] = new JsonObject
            {
                [fieldName] = value
            }
        });
    }
}
