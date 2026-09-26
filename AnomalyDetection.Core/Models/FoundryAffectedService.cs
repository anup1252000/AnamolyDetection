using System.Text.Json.Serialization;

namespace AnomalyDetection.Core.Models;

public sealed class FoundryAffectedService
{
    [JsonPropertyName("service")]
    public string Service { get; init; } = string.Empty;

    [JsonPropertyName("environment")]
    public string Environment { get; init; } = string.Empty;

    [JsonPropertyName("notes")]
    public string Notes { get; init; } = string.Empty;
}
