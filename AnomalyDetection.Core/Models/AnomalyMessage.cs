namespace AnomalyDetection.Core.Models;

public sealed class AnomalyMessage
{
    public string Id { get; init; } = string.Empty;
    public string JobId { get; init; } = string.Empty;
    public string Detector { get; init; } = string.Empty;
    public double AnomalyScore { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string Service { get; init; } = string.Empty;
    public string Environment { get; init; } = string.Empty;
    public string? Influencer { get; init; }
    public string ResultType { get; init; } = string.Empty;
    public IReadOnlyList<double> Actual { get; init; } = [];
    public IReadOnlyList<double> Typical { get; init; } = [];
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
}
