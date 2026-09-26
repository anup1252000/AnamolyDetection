using System.Text.Json;
using AnomalyDetection.Infrastructure.Foundry;

namespace AnomalyDetection.Infrastructure.Tests;

public sealed class FoundryResponseParserTests
{
    [Fact]
    public void TryParse_ReturnsStructuredResponseForOutputText()
    {
        using var document = JsonDocument.Parse("""
        {
          "output_text": "{\"severity\":\"High\",\"rootCause\":\"Database saturation\",\"confidence\":0.8,\"observedFacts\":[\"p95 latency rose\"],\"hypotheses\":[{\"description\":\"Connection pool saturation\",\"confidence\":0.7,\"evidence\":[\"timeouts increased\"]}],\"affectedServices\":[{\"service\":\"orders-api\",\"environment\":\"prod\",\"notes\":\"Primary impact\"}],\"evidence_supporting_conclusion\":[\"DB CPU peaked\"],\"recommendedRemediations\":[\"scale database\"],\"explanation\":\"Evidence points to DB pressure\",\"notes\":\"Validate DB pool settings\"}"
        }
        """);

        var parsed = FoundryResponseParser.TryParse(document.RootElement, out var response);

        Assert.True(parsed);
        Assert.Equal("High", response.Severity);
        Assert.Equal("Database saturation", response.RootCause);
        Assert.Single(response.Hypotheses);
        Assert.Single(response.AffectedServices);
        Assert.Single(response.EvidenceSupportingConclusion);
        Assert.Single(response.RecommendedRemediations);
    }

    [Fact]
    public void TryParse_ReturnsStructuredResponseForOutputArrayText()
    {
        using var document = JsonDocument.Parse("""
        {
          "output": [
            {
              "content": [
                {
                  "text": "{\"severity\":\"Medium\",\"rootCause\":\"Transient dependency latency\",\"confidence\":0.6,\"observedFacts\":[\"upstream latency increased\"],\"hypotheses\":[],\"affectedServices\":[],\"evidence_supporting_conclusion\":[\"dependency p95 increased\"],\"recommendedRemediations\":[\"throttle burst traffic\"],\"explanation\":\"Dependency latency observed\",\"notes\":\"Monitor upstream\"}"
                }
              ]
            }
          ]
        }
        """);

        var parsed = FoundryResponseParser.TryParse(document.RootElement, out var response);

        Assert.True(parsed);
        Assert.Equal("Medium", response.Severity);
        Assert.Equal("Transient dependency latency", response.RootCause);
        Assert.Single(response.ObservedFacts);
    }

    [Fact]
    public void TryParse_ReturnsFalseForInvalidJsonPayload()
    {
        using var document = JsonDocument.Parse("""
        {
          "output_text": "not-json"
        }
        """);

        var parsed = FoundryResponseParser.TryParse(document.RootElement, out _);

        Assert.False(parsed);
    }
}
