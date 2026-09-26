namespace AnomalyDetection.Core.Configuration;

public sealed class SourceFieldsOptions
{
    public const string SectionName = "SourceFields";

    public string Timestamp { get; init; } = "@timestamp";
    public string Service { get; init; } = "service.name";
    public string Environment { get; init; } = "service.environment";
    public string Duration { get; init; } = "event.duration";
    public string StatusCode { get; init; } = "http.response.status_code";
    public string Route { get; init; } = "url.path";
    public string Message { get; init; } = "message";
    public string Exception { get; init; } = "error.message";
    public string TraceId { get; init; } = "trace.id";
    public string[] AdditionalFields { get; init; } = [];
}
