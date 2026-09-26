using System.Text;
using System.Text.Json;
using AnomalyDetection.Core.Configuration;
using AnomalyDetection.Core.Models;
using AnomalyDetection.Core.Services;
using AnomalyDetection.Infrastructure.RabbitMq;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AnomalyDetection.Worker;

public sealed class RcaWorker : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RabbitMqConnectionProvider _connectionProvider;
    private readonly RabbitMqOptions _options;
    private readonly IAnomalyInvestigationProcessor _processor;
    private readonly ILogger<RcaWorker> _logger;
    private IModel? _channel;
    private string? _consumerTag;

    public RcaWorker(
        RabbitMqConnectionProvider connectionProvider,
        IOptions<RabbitMqOptions> options,
        IAnomalyInvestigationProcessor processor,
        ILogger<RcaWorker> logger)
    {
        _connectionProvider = connectionProvider;
        _options = options.Value;
        _processor = processor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = _connectionProvider.GetConnection();
        _channel = connection.CreateModel();
        _channel.ExchangeDeclare(_options.Exchange, ExchangeType.Direct, durable: true, autoDelete: false);
        _channel.QueueDeclare(_options.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueDeclare(
            queue: _options.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = _options.DeadLetterQueue
            });
        _channel.QueueBind(_options.Queue, _options.Exchange, _options.RoutingKey);
        _channel.BasicQos(0, _options.PrefetchCount, false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (_, eventArgs) => await HandleMessageAsync(eventArgs, stoppingToken);
        _consumerTag = _channel.BasicConsume(_options.Queue, autoAck: false, consumer);

        _logger.LogInformation("RabbitMQ consumer started for queue {Queue}", _options.Queue);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("RabbitMQ consumer stopping");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true } && !string.IsNullOrWhiteSpace(_consumerTag))
        {
            _channel.BasicCancel(_consumerTag);
        }

        _channel?.Close();
        _channel?.Dispose();
        _channel = null;
        return base.StopAsync(cancellationToken);
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs eventArgs, CancellationToken stoppingToken)
    {
        if (_channel is null)
        {
            return;
        }

        var message = DeserializeMessage(eventArgs.Body.ToArray());
        if (message is null)
        {
            _logger.LogWarning("Rejecting malformed anomaly message from queue {Queue}", _options.Queue);
            _channel.BasicReject(eventArgs.DeliveryTag, requeue: false);
            return;
        }

        for (var attempt = 1; attempt <= Math.Max(1, _options.MaxRetryAttempts); attempt++)
        {
            try
            {
                await _processor.ProcessAsync(message, stoppingToken);
                _channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
                return;
            }
            catch (Exception exception) when (IsTransient(exception) && attempt < Math.Max(1, _options.MaxRetryAttempts))
            {
                _logger.LogWarning(
                    exception,
                    "Transient failure processing anomaly {AnomalyId}; retry {Attempt} of {MaxAttempts}",
                    message.Id,
                    attempt,
                    _options.MaxRetryAttempts);

                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.RetryDelaySeconds)), stoppingToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Dead-lettering anomaly {AnomalyId} after processing failure",
                    message.Id);
                _channel.BasicReject(eventArgs.DeliveryTag, requeue: false);
                return;
            }
        }
    }

    private static AnomalyMessage? DeserializeMessage(ReadOnlyMemory<byte> payload)
    {
        try
        {
            return JsonSerializer.Deserialize<AnomalyMessage>(payload.Span, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsTransient(Exception exception)
    {
        return exception is HttpRequestException or TimeoutException;
    }
}
