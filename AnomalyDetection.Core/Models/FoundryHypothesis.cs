using System.Text.Json.Serialization;

namespace AnomalyDetection.Core.Models;

public sealed class FoundryHypothesis
{
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }

    [JsonPropertyName("evidence")]
    public IReadOnlyList<string> Evidence { get; init; } = [];
}
