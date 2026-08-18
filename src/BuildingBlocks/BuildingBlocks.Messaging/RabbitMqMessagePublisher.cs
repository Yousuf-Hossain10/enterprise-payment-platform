using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Publishes to the topic exchange declared in <see cref="RabbitMqOptions"/>, using
/// the event's Type as the routing key so consumers (Notification, Audit) can bind
/// on exactly the events they care about. Connection/channel are created lazily on
/// first publish and reused - see ADR-0001 in docs/adr/ for why RabbitMQ.
/// </summary>
public sealed class RabbitMqMessagePublisher : IMessagePublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqMessagePublisher(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    public async Task PublishAsync(Guid messageId, string type, string payload, CancellationToken cancellationToken)
    {
        var channel = await GetChannelAsync(cancellationToken);

        var body = Encoding.UTF8.GetBytes(payload);
        var properties = new BasicProperties
        {
            Persistent = true,
            Type = type,
            MessageId = messageId.ToString()
        };

        await channel.BasicPublishAsync(
            exchange: _options.ExchangeName,
            routingKey: type,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
            return _channel;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true })
                return _channel;

            _connection ??= await new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password
            }.CreateConnectionAsync(cancellationToken);

            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await _channel.ExchangeDeclareAsync(
                _options.ExchangeName, ExchangeType.Topic, durable: true, cancellationToken: cancellationToken);

            return _channel;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.CloseAsync();
        if (_connection is not null)
            await _connection.CloseAsync();
        _connectionLock.Dispose();
    }
}
