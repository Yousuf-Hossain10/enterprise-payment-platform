using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Declares a durable queue, binds it to the shared topic exchange
/// (RabbitMqOptions.ExchangeName) for each configured routing key
/// (RabbitMqConsumerOptions.RoutingKeys), and dispatches each delivery to a
/// scoped IEventHandler the implementing service registers - mirrors
/// OutboxDispatcherBackgroundService's role on the publish side, but push-based
/// (RabbitMQ delivers messages via the consumer's ReceivedAsync event) rather than
/// poll-based, since that's how AMQP consumption actually works.
///
/// A handler exception nacks-and-requeues the delivery rather than dropping it -
/// without a dead-letter queue configured yet (Day 41), an immediate infinite
/// retry loop is the safer default failure mode than silently losing the message.
/// </summary>
public sealed class RabbitMqConsumerBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly RabbitMqConsumerOptions _consumerOptions;
    private readonly ILogger<RabbitMqConsumerBackgroundService> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqConsumerBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> rabbitMqOptions,
        IOptions<RabbitMqConsumerOptions> consumerOptions,
        ILogger<RabbitMqConsumerBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _rabbitMqOptions = rabbitMqOptions.Value;
        _consumerOptions = consumerOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _connection = await new ConnectionFactory
        {
            HostName = _rabbitMqOptions.HostName,
            Port = _rabbitMqOptions.Port,
            UserName = _rabbitMqOptions.UserName,
            Password = _rabbitMqOptions.Password
        }.CreateConnectionAsync(stoppingToken);

        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // Declared here too (not just by publishers) so a consumer that starts
        // before any publisher has run still has something real to bind to.
        await _channel.ExchangeDeclareAsync(
            _rabbitMqOptions.ExchangeName, ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(
            _consumerOptions.QueueName, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: stoppingToken);

        foreach (var routingKey in _consumerOptions.RoutingKeys)
        {
            await _channel.QueueBindAsync(
                _consumerOptions.QueueName, _rabbitMqOptions.ExchangeName, routingKey, cancellationToken: stoppingToken);
        }

        // Cap in-flight unacked deliveries per consumer so one slow handler can't
        // let RabbitMQ push unbounded work at this process.
        await _channel.BasicQosAsync(0, prefetchCount: 10, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, ea) => OnMessageReceivedAsync(ea, stoppingToken);

        await _channel.BasicConsumeAsync(
            _consumerOptions.QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Consuming queue {QueueName} bound to routing keys [{RoutingKeys}].",
            _consumerOptions.QueueName, string.Join(", ", _consumerOptions.RoutingKeys));

        // BasicConsumeAsync registers a push subscription and returns immediately -
        // this keeps the hosted service alive until shutdown is requested.
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    private async Task OnMessageReceivedAsync(BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        var messageId = Guid.TryParse(ea.BasicProperties.MessageId, out var parsed) ? parsed : Guid.Empty;
        var type = ea.BasicProperties.Type ?? "Unknown";
        var payload = Encoding.UTF8.GetString(ea.Body.ToArray());

        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IEventHandler>();

        try
        {
            await handler.HandleAsync(messageId, type, payload, stoppingToken);
            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Failed to handle {EventType} message {MessageId}; nacking for redelivery.",
                type, messageId);
            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
            await _channel.CloseAsync(cancellationToken);
        if (_connection is not null)
            await _connection.CloseAsync(cancellationToken);

        await base.StopAsync(cancellationToken);
    }
}
