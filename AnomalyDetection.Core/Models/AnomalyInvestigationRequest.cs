using System.ComponentModel.DataAnnotations;

namespace AnomalyDetection.Core.Models;

public sealed class AnomalyInvestigationRequest
{
    [Required]
    [MaxLength(200)]
    public string JobId { get; init; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Detector { get; init; } = string.Empty;

    [Range(0, 100)]
    public double AnomalyScore { get; init; }

    [Required]
    public DateTimeOffset Timestamp { get; init; }

    [Required]
    [MaxLength(200)]
    public string Service { get; init; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Environment { get; init; } = string.Empty;

    [MaxLength(200)]
    public string? Influencer { get; init; }

    [Required]
    [MaxLength(100)]
    public string ResultType { get; init; } = string.Empty;

    public IReadOnlyList<double> Actual { get; init; } = [];

    public IReadOnlyList<double> Typical { get; init; } = [];
}
