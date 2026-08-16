namespace BuildingBlocks.Messaging;

/// <summary>
/// Each service implements this against its own DbContext, same as
/// <see cref="IOutboxStore"/> - this library owns no DbContext itself.
///
/// <see cref="IsProcessedAsync"/> is a cheap pre-check to skip obviously-duplicate
/// work before running a handler. It is NOT, on its own, a guarantee against two
/// concurrent deliveries of the same event both passing the check before either
/// marks it processed. The actual safety net is at the implementer's discretion:
/// write the <see cref="ProcessedEvent"/> row in the SAME transaction as the
/// handler's business-logic writes (same DbContext.SaveChangesAsync() call), with
/// a unique constraint on EventId, and treat a duplicate-key failure on that
/// insert as "another delivery already handled this - discard silently."
/// </summary>
public interface IProcessedEventStore
{
    Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken);

    Task MarkProcessedAsync(Guid eventId, DateTime processedAtUtc, CancellationToken cancellationToken);
}
