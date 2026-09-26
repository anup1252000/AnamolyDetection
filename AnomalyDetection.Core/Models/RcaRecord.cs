using System.Text.Json.Serialization;

namespace AnomalyDetection.Core.Models;

public sealed class RcaRecord
{
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("@timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    public string JobId { get; init; } = string.Empty;
    public string Detector { get; init; } = string.Empty;
    public double AnomalyScore { get; init; }
    public string Service { get; init; } = string.Empty;
    public string Environment { get; init; } = string.Empty;
    public string Severity { get; init; } = SeverityLevels.Unknown;
    public string RootCause { get; init; } = "Insufficient evidence";
    public double Confidence { get; init; }
    public string Explanation { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public string? Influencer { get; init; }
    public string ResultType { get; init; } = string.Empty;
    public IReadOnlyList<double> Actual { get; init; } = [];
    public IReadOnlyList<double> Typical { get; init; } = [];
    public IReadOnlyList<string> ObservedFacts { get; init; } = [];
    public IReadOnlyList<FoundryHypothesis> Hypotheses { get; init; } = [];
    public IReadOnlyList<FoundryAffectedService> AffectedServices { get; init; } = [];

    [JsonPropertyName("evidence_supporting_conclusion")]
    public IReadOnlyList<string> EvidenceSupportingConclusion { get; init; } = [];

    public IReadOnlyList<string> RecommendedRemediations { get; init; } = [];
}
