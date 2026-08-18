using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Polls <see cref="IOutboxStore"/> on an interval, publishes each unprocessed
/// message via <see cref="IMessagePublisher"/>, and marks it processed. A single
/// message's publish failure is logged and left unprocessed for the next poll
/// (at-least-once delivery - the same guarantee RabbitMQ itself makes, which is
/// why consumers need the idempotent-consumer pattern, docs/Coding-Standards.md).
/// </summary>
public sealed class OutboxDispatcherBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMessagePublisher _publisher;
    private readonly OutboxDispatcherOptions _options;
    private readonly ILogger<OutboxDispatcherBackgroundService> _logger;

    public OutboxDispatcherBackgroundService(
        IServiceScopeFactory scopeFactory,
        IMessagePublisher publisher,
        IOptions<OutboxDispatcherOptions> options,
        ILogger<OutboxDispatcherBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.PollInterval);
        do
        {
            await DispatchBatchAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task DispatchBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

        var messages = await store.GetUnprocessedAsync(_options.BatchSize, cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await _publisher.PublishAsync(message.Id, message.Type, message.Payload, cancellationToken);
                await store.MarkProcessedAsync(message.Id, DateTime.UtcNow, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(
                    ex,
                    "Failed to dispatch outbox message {OutboxMessageId} of type {OutboxMessageType}; will retry next poll.",
                    message.Id,
                    message.Type);
            }
        }
    }
}
