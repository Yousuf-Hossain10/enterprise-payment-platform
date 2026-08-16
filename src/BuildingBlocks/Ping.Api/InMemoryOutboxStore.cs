using BuildingBlocks.Messaging;

namespace Ping.Api;

/// <summary>
/// Throwaway in-memory IOutboxStore, only so AddOutboxDispatcher() has something
/// to resolve - real services implement this against their own DbContext (see
/// BuildingBlocks.Messaging's README). Always empty, so the dispatcher never
/// actually needs a live RabbitMQ connection during this composition proof.
/// </summary>
public class InMemoryOutboxStore : IOutboxStore
{
    public Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OutboxMessage>>([]);

    public Task MarkProcessedAsync(Guid id, DateTime processedAtUtc, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
