namespace AnomalyDetection.Core.Configuration;

public sealed class FoundryOptions
{
    public const string SectionName = "Foundry";

    public string ProjectEndpoint { get; init; } = string.Empty;
    public string AgentName { get; init; } = string.Empty;
    public string ApiVersion { get; init; } = "2025-11-15-preview";
    public int TimeoutSeconds { get; init; } = 60;
    public int MaxRetries { get; init; } = 3;
    public int RetryDelaySeconds { get; init; } = 2;
}
