namespace BuildingBlocks.Messaging;

/// <summary>
/// Each service implements this against its own DbContext (this library has no
/// opinion on which database or ORM a service uses) - the dispatcher only ever
/// talks to this abstraction, never a concrete DbContext.
/// </summary>
public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken);

    Task MarkProcessedAsync(Guid id, DateTime processedAtUtc, CancellationToken cancellationToken);
}
