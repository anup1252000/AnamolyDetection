using System.Text.Json.Serialization;

namespace AnomalyDetection.Core.Models;

public sealed class FoundryResponse
{
    [JsonPropertyName("severity")]
    public string Severity { get; init; } = SeverityLevels.Unknown;

    [JsonPropertyName("rootCause")]
    public string RootCause { get; init; } = "Insufficient evidence";

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }

    [JsonPropertyName("evidence")]
    public IReadOnlyList<string> Evidence { get; init; } = [];

    [JsonPropertyName("recommendations")]
    public IReadOnlyList<string> Recommendations { get; init; } = [];

    [JsonPropertyName("affectedServices")]
    public IReadOnlyList<string> AffectedServices { get; init; } = [];

    [JsonPropertyName("explanation")]
    public string Explanation { get; init; } = string.Empty;
}
