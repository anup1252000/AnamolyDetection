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
          "output_text": "{\"severity\":\"High\",\"rootCause\":\"Database saturation\",\"confidence\":0.8,\"evidence\":[\"p95 latency rose\"],\"recommendations\":[\"scale database\"],\"affectedServices\":[\"orders-api\"],\"explanation\":\"Evidence points to DB pressure\"}"
        }
        """);

        var parsed = FoundryResponseParser.TryParse(document.RootElement, out var response);

        Assert.True(parsed);
        Assert.Equal("High", response.Severity);
        Assert.Equal("Database saturation", response.RootCause);
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
