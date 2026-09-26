namespace AnomalyDetection.Core.Configuration;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 5672;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string VirtualHost { get; init; } = "/";
    public string Exchange { get; init; } = "anomaly.events";
    public string Queue { get; init; } = "anomaly-investigation";
    public string RoutingKey { get; init; } = "anomaly.detected";
    public string DeadLetterQueue { get; init; } = "anomaly-investigation.dlq";
    public ushort PrefetchCount { get; init; } = 10;
    public int MaxRetryAttempts { get; init; } = 3;
    public int RetryDelaySeconds { get; init; } = 5;
}
