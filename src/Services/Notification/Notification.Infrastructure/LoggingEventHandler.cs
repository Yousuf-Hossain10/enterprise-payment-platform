using BuildingBlocks.Messaging;
using Microsoft.Extensions.Logging;

namespace Notification.Infrastructure;

/// <summary>
/// Day 39 scaffold: logs receipt of every event this service is bound to
/// (PaymentCaptured, PaymentFailed, WalletDebited, WalletCredited) and
/// acknowledges it. No idempotency check, no actual notification delivery yet -
/// both are Day 40's job (ProcessedEvents + mock email/SMS templates). This proves
/// the consumer scaffold (queue binding, message delivery, ack/nack) actually works
/// end-to-end before any business logic is layered on top of it.
/// </summary>
public class LoggingEventHandler : IEventHandler
{
    private readonly ILogger<LoggingEventHandler> _logger;

    public LoggingEventHandler(ILogger<LoggingEventHandler> logger) => _logger = logger;

    public Task HandleAsync(Guid messageId, string type, string payload, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received {EventType} message {MessageId}: {Payload}", type, messageId, payload);
        return Task.CompletedTask;
    }
}
