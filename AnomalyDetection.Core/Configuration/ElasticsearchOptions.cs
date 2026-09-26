namespace AnomalyDetection.Core.Configuration;

public sealed class ElasticsearchOptions
{
    public const string SectionName = "Elasticsearch";

    public string Url { get; init; } = string.Empty;
    public string? ApiKey { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string SourceIndex { get; init; } = string.Empty;
    public string AnomalyResultsIndex { get; init; } = "ai-anomalies-*";
    public int EvidenceWindowMinutes { get; init; } = 15;
    public int EvidenceSizeLimit { get; init; } = 100;
}
