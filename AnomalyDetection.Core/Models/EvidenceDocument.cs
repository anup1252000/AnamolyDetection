namespace AnomalyDetection.Core.Models;

public sealed class EvidenceDocument
{
    public DateTimeOffset? Timestamp { get; init; }
    public string? Service { get; init; }
    public string? Environment { get; init; }
    public string? Route { get; init; }
    public double? Duration { get; init; }
    public int? StatusCode { get; init; }
    public string? Message { get; init; }
    public string? Exception { get; init; }
    public string? TraceId { get; init; }
    public IReadOnlyDictionary<string, string?> AdditionalFields { get; init; } = new Dictionary<string, string?>();
}
