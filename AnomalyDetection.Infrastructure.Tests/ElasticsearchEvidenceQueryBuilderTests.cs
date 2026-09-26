using System.Text.Json.Nodes;
using AnomalyDetection.Core.Configuration;
using AnomalyDetection.Core.Models;
using AnomalyDetection.Infrastructure.Elasticsearch;

namespace AnomalyDetection.Infrastructure.Tests;

public sealed class ElasticsearchEvidenceQueryBuilderTests
{
    [Fact]
    public void Build_CreatesBoundedEvidenceQueryWithConfiguredFields()
    {
        var builder = new ElasticsearchEvidenceQueryBuilder(
            new ElasticsearchOptions
            {
                EvidenceWindowMinutes = 10,
                EvidenceSizeLimit = 25
            },
            new SourceFieldsOptions
            {
                Timestamp = "@timestamp",
                Service = "service.name",
                Environment = "service.environment",
                Route = "url.path",
                Duration = "event.duration",
                StatusCode = "http.response.status_code",
                Message = "message",
                Exception = "error.message",
                TraceId = "trace.id",
                AdditionalFields = ["deployment.version"]
            });

        var payload = builder.Build(new AnomalyMessage
        {
            Id = "1",
            Timestamp = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero),
            Service = "checkout",
            Environment = "prod",
            Influencer = "trace-123"
        });

        Assert.Equal(25, payload["size"]!.GetValue<int>());

        var sourceFields = payload["_source"]!.AsArray().Select(node => node!.GetValue<string>()).ToArray();
        Assert.Contains("deployment.version", sourceFields);

        var mustClauses = payload["query"]!["bool"]!["must"]!.AsArray();
        Assert.True(mustClauses.Count >= 3);

        var rangeClause = mustClauses[0]!["range"]!["@timestamp"] as JsonObject;
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 9, 50, 0, TimeSpan.Zero), rangeClause!["gte"]!.GetValue<DateTimeOffset>());
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 10, 10, 0, TimeSpan.Zero), rangeClause!["lte"]!.GetValue<DateTimeOffset>());
    }
}
