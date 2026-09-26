namespace AnomalyDetection.Core.Configuration;

public sealed class AnomalyDetectionOptions
{
    public const string SectionName = "AnomalyDetection";

    public string JobId { get; init; } = string.Empty;
    public string DatafeedId { get; init; } = string.Empty;
    public string BucketSpan { get; init; } = "15m";
    public string[] Influencers { get; init; } = [];
    public DetectorOptions[] Detectors { get; init; } = [];
}

public sealed class DetectorOptions
{
    public string Id { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Function { get; init; } = string.Empty;
    public string? FieldName { get; init; }
    public string? ByFieldName { get; init; }
    public string? OverFieldName { get; init; }
    public string? PartitionFieldName { get; init; }
}
