using System.Text;
using System.Text.Json;
using AnomalyDetection.Core.Configuration;
using AnomalyDetection.Core.Models;
using AnomalyDetection.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace AnomalyDetection.Infrastructure.RabbitMq;

public sealed class AnomalyPublisher : IAnomalyPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RabbitMqConnectionProvider _connectionProvider;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<AnomalyPublisher> _logger;

    public AnomalyPublisher(
        RabbitMqConnectionProvider connectionProvider,
        IOptions<RabbitMqOptions> options,
        ILogger<AnomalyPublisher> logger)
    {
        _connectionProvider = connectionProvider;
        _options = options.Value;
        _logger = logger;
    }

    public Task PublishAsync(AnomalyMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var connection = _connectionProvider.GetConnection();
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare(_options.Exchange, ExchangeType.Direct, durable: true, autoDelete: false);
        channel.QueueDeclare(_options.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false);
        channel.QueueDeclare(
            queue: _options.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = _options.DeadLetterQueue
            });
        channel.QueueBind(_options.Queue, _options.Exchange, _options.RoutingKey);

        var payload = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.MessageId = message.Id;
        properties.ContentType = "application/json";
        properties.Timestamp = new AmqpTimestamp(message.ReceivedAt.ToUnixTimeSeconds());

        channel.ConfirmSelect();
        channel.BasicPublish(_options.Exchange, _options.RoutingKey, mandatory: true, basicProperties: properties, body: payload);
        channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));

        _logger.LogInformation(
            "Anomaly published to exchange {Exchange} with routing key {RoutingKey} for job {JobId}",
            _options.Exchange,
            _options.RoutingKey,
            message.JobId);

        return Task.CompletedTask;
    }
}
