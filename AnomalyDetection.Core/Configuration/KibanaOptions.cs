namespace AnomalyDetection.Core.Configuration;

public sealed class KibanaOptions
{
    public const string SectionName = "Kibana";

    public string Url { get; init; } = string.Empty;
    public string? ApiKey { get; init; }
    public string Space { get; init; } = "default";
    public string DataViewName { get; init; } = "Telemetry";
    public string? DataViewId { get; init; }
    public string ResultsDataViewName { get; init; } = "AI Anomalies";
    public string? ResultsDataViewId { get; init; }
    public string DashboardTitle { get; init; } = "Anomaly RCA Dashboard";
    public string? DashboardId { get; init; }
}
