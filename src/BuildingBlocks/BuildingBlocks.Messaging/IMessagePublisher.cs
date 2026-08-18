namespace BuildingBlocks.Messaging;

public interface IMessagePublisher
{
    /// <param name="messageId">
    /// The outbox message's own Id, propagated as the AMQP message's stable
    /// MessageId property - what a consumer's idempotent-consumer check (Phase 8,
    /// IdempotentEventDispatcher) dedupes on across RabbitMQ's at-least-once
    /// redelivery. Not previously wired (this parameter didn't exist before Day 39),
    /// which meant a consumer had no stable identifier to key a ProcessedEvents
    /// check on at all.
    /// </param>
    Task PublishAsync(Guid messageId, string type, string payload, CancellationToken cancellationToken);
}
