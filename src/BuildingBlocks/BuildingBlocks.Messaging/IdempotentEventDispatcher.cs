using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Wraps a RabbitMQ consumer's handler invocation with the idempotent-consumer
/// check: skip if this event was already processed, otherwise run the handler and
/// record it. Handler exceptions are never swallowed here - they propagate so the
/// consumer's own retry/DLQ policy (Phase 8) decides what happens next; this class
/// only prevents *successful* handling from happening twice.
/// </summary>
public class IdempotentEventDispatcher
{
    private readonly IProcessedEventStore _store;
    private readonly ILogger<IdempotentEventDispatcher> _logger;

    public IdempotentEventDispatcher(IProcessedEventStore store, ILogger<IdempotentEventDispatcher> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <returns>
    /// <see langword="true"/> if the handler ran, <see langword="false"/> if the
    /// event was already processed and the handler was skipped.
    /// </returns>
    public async Task<bool> HandleAsync(Guid eventId, Func<CancellationToken, Task> handler, CancellationToken cancellationToken)
    {
        if (await _store.IsProcessedAsync(eventId, cancellationToken))
        {
            _logger.LogInformation("Event {EventId} already processed, skipping (idempotent consumer).", eventId);
            return false;
        }

        await handler(cancellationToken);
        await _store.MarkProcessedAsync(eventId, DateTime.UtcNow, cancellationToken);
        return true;
    }
}
