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

    [JsonPropertyName("observedFacts")]
    public IReadOnlyList<string> ObservedFacts { get; init; } = [];

    [JsonPropertyName("hypotheses")]
    public IReadOnlyList<FoundryHypothesis> Hypotheses { get; init; } = [];

    [JsonPropertyName("affectedServices")]
    public IReadOnlyList<FoundryAffectedService> AffectedServices { get; init; } = [];

    [JsonPropertyName("evidence_supporting_conclusion")]
    public IReadOnlyList<string> EvidenceSupportingConclusion { get; init; } = [];

    [JsonPropertyName("recommendedRemediations")]
    public IReadOnlyList<string> RecommendedRemediations { get; init; } = [];

    [JsonPropertyName("explanation")]
    public string Explanation { get; init; } = string.Empty;

    [JsonPropertyName("notes")]
    public string Notes { get; init; } = string.Empty;
}
