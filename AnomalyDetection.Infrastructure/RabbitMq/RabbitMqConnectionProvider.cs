using AnomalyDetection.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace AnomalyDetection.Infrastructure.RabbitMq;

public sealed class RabbitMqConnectionProvider : IDisposable
{
    private readonly ConnectionFactory _connectionFactory;
    private readonly ILogger<RabbitMqConnectionProvider> _logger;
    private IConnection? _connection;
    private readonly object _syncRoot = new();

    public RabbitMqConnectionProvider(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConnectionProvider> logger)
    {
        var settings = options.Value;
        _logger = logger;
        _connectionFactory = new ConnectionFactory
        {
            HostName = settings.Host,
            Port = settings.Port,
            UserName = settings.Username,
            Password = settings.Password,
            VirtualHost = settings.VirtualHost,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        };
    }

    public IConnection GetConnection()
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        lock (_syncRoot)
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            _connection?.Dispose();
            _connection = _connectionFactory.CreateConnection();
            _logger.LogInformation("RabbitMQ connection established to host {Host}", _connectionFactory.HostName);
            return _connection;
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
