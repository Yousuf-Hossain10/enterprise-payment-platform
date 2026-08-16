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

**Testing note:** the dispatcher's own logic (publish-then-mark, continue-past-a-single-failure, batch size propagation, and the `ExecuteAsync` polling loop itself) is unit tested against fake `IOutboxStore`/`IMessagePublisher` — no real broker involved. `RabbitMqMessagePublisher` itself was manually verified against the live local RabbitMQ (Days 8-10) during development, but per `docs/Coding-Standards.md`, real broker/database integration tests as a permanent part of the suite start in Phase 5 via Testcontainers, not before.

## `ProcessedEvent` + `IProcessedEventStore` (Day 14)

The consuming-side counterpart to the outbox: a row per event a service has already handled, so a redelivered or duplicate message under RabbitMQ's at-least-once guarantee is a no-op instead of a repeated side effect. Same shape as `IOutboxStore` — each service implements `IProcessedEventStore` against its own DbContext.

`IsProcessedAsync` is a cheap pre-check, **not** on its own a guarantee against two concurrent deliveries of the same event both passing the check before either marks it processed. The real safety net is the implementer's responsibility: write the `ProcessedEvent` row in the *same transaction* as the handler's business-logic writes (one `DbContext.SaveChangesAsync()` call), with a unique constraint on `EventId` — a duplicate-key failure on that insert means another delivery already handled it, and can be discarded silently. This library provides the shape and the convenience dispatcher below; the transactional guarantee is necessarily each service's own, since this library owns no DbContext.

## `IdempotentEventDispatcher` + `AddIdempotentEventConsumer()` (Day 14)

Wraps a consumer's handler invocation with the idempotent-consumer check:

```csharp
builder.Services.AddIdempotentEventConsumer();
builder.Services.AddScoped<IProcessedEventStore, NotificationProcessedEventStore>();

// inside the RabbitMQ consumer callback (Phase 8):
var wasHandled = await dispatcher.HandleAsync(eventId, async ct =>
{
    await SendNotificationAsync(payload, ct);
}, cancellationToken);
```

Returns `true` if the handler ran, `false` if the event was already processed and the handler was skipped. **Handler exceptions are never swallowed** — they propagate so the consumer's own retry/DLQ policy (Phase 8) decides what happens next; this class only prevents a *successful* handling from happening twice.

## Coming Later

RabbitMQ consumer scaffolding and dead-letter queue handling are built per-service starting Phase 8 (Notification) — this library provides the idempotency primitive they'll use, not the consumer host itself.
