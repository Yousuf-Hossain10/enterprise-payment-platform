using System.Collections.Concurrent;
using BuildingBlocks.Messaging;

namespace Ping.Api;

/// <summary>
/// Throwaway in-memory IProcessedEventStore, only so AddIdempotentEventConsumer()
/// has something to resolve - real services implement this against their own
/// DbContext (see BuildingBlocks.Messaging's README).
/// </summary>
public class InMemoryProcessedEventStore : IProcessedEventStore
{
    private static readonly ConcurrentDictionary<Guid, DateTime> Processed = new();

    public Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken) =>
        Task.FromResult(Processed.ContainsKey(eventId));

    public Task MarkProcessedAsync(Guid eventId, DateTime processedAtUtc, CancellationToken cancellationToken)
    {
        Processed[eventId] = processedAtUtc;
        return Task.CompletedTask;
    }
}
