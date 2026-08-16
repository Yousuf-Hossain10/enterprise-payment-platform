namespace BuildingBlocks.Messaging;

/// <summary>
/// Written to the same database transaction as the business change it describes
/// (see docs/Architecture.md's "Asynchronous (RabbitMQ)" section) - never
/// constructed and published directly, only ever persisted via the owning
/// service's DbContext and picked up by <see cref="OutboxDispatcherBackgroundService"/>.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = default!;
    public string Payload { get; set; } = default!;
    public DateTime OccurredAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
}
