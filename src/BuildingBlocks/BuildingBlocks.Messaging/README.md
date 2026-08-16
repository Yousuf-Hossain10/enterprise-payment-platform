# BuildingBlocks.Messaging

The transactional outbox pattern, per `docs/Architecture.md`'s "Asynchronous (RabbitMQ)" section: a domain event is written to the same database transaction as the business change, then a background dispatcher publishes it to RabbitMQ — so a crash between "state changed" and "event published" can never happen.

Built incrementally across Phase 4 (Days 13-14); this README covers what exists as of each day.

## `OutboxMessage` (Day 13)

The row shape every service's `Infrastructure` layer persists in its own DbContext, in the same transaction as the business write:

```csharp
_db.OutboxMessages.Add(new OutboxMessage
{
    Id = Guid.NewGuid(),
    Type = nameof(WalletDebited),
    Payload = JsonSerializer.Serialize(new WalletDebited(accountId, amount, correlationId)),
    OccurredAtUtc = DateTime.UtcNow
});
await _db.SaveChangesAsync(); // ledger entry + outbox row commit atomically
```

This library doesn't define an `IEntityTypeConfiguration` or own any DbContext — each service maps `OutboxMessage` into its own EF Core model, since this library has no opinion on which database a service uses.

## `IOutboxStore` (Day 13)

The abstraction the dispatcher depends on. Each service implements this against its own DbContext:

```csharp
public class WalletOutboxStore(WalletDbContext db) : IOutboxStore
{
    public Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(int batchSize, CancellationToken ct) =>
        db.OutboxMessages.Where(m => m.ProcessedAtUtc == null)
            .OrderBy(m => m.OccurredAtUtc).Take(batchSize).ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<OutboxMessage>)t.Result, ct);

    public async Task MarkProcessedAsync(Guid id, DateTime processedAtUtc, CancellationToken ct)
    {
        var message = await db.OutboxMessages.FindAsync([id], ct);
        message!.ProcessedAtUtc = processedAtUtc;
        await db.SaveChangesAsync(ct);
    }
}
```

## `OutboxDispatcherBackgroundService` + `AddOutboxDispatcher()` (Day 13)

A `BackgroundService` that polls `IOutboxStore` on an interval (`OutboxDispatcherOptions.PollInterval`, default 5s), publishes each unprocessed message via `IMessagePublisher` (RabbitMQ, topic exchange, routing key = event type), and marks it processed. A single message's publish failure is logged and left unprocessed — picked up again next poll, matching RabbitMQ's own at-least-once guarantee.

```csharp
builder.Services.AddOutboxDispatcher();
builder.Services.AddScoped<IOutboxStore, WalletOutboxStore>(); // each service registers its own
```

Configuration (bound + validated on startup via `BuildingBlocks.Common`'s `AddValidatedOptions`):

```json
{
  "RabbitMq": { "HostName": "rabbitmq.platform.svc.cluster.local", "UserName": "...", "Password": "..." },
  "OutboxDispatcher": { "PollInterval": "00:00:05", "BatchSize": 50 }
}
```

**Testing note:** the dispatcher's own logic (publish-then-mark, continue-past-a-single-failure) is unit tested against fake `IOutboxStore`/`IMessagePublisher` — no real broker involved. `RabbitMqMessagePublisher` itself was manually verified against the live local RabbitMQ (Days 8-10) during development, but per `docs/Coding-Standards.md`, real broker/database integration tests as a permanent part of the suite start in Phase 5 via Testcontainers, not before.

## Coming in Day 14

The idempotent-consumer helper (`ProcessedEvents` pattern) for the consuming side, and additional outbox unit tests.
