namespace BuildingBlocks.Messaging;

/// <summary>
/// The row shape a consuming service persists per handled event, so a redelivered
/// or duplicate message under RabbitMQ's at-least-once guarantee is a no-op instead
/// of a repeated side effect. See docs/Coding-Standards.md's idempotent-consumer
/// pattern and <see cref="IProcessedEventStore"/> for the transactional contract.
/// </summary>
public class ProcessedEvent
{
    public Guid EventId { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
}
