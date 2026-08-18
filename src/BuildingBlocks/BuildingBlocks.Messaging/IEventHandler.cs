namespace BuildingBlocks.Messaging;

/// <summary>
/// The service-specific extension point RabbitMqConsumerBackgroundService invokes
/// per delivered message - mirrors IOutboxStore's role on the publish side (this
/// library owns the RabbitMQ mechanics, the implementing service owns what a
/// message actually means). An unhandled exception here nacks-and-requeues the
/// delivery rather than acking it; whether that's the right failure policy for a
/// message that keeps failing (vs. dead-lettering it) is Day 41's job, not this
/// interface's.
/// </summary>
public interface IEventHandler
{
    Task HandleAsync(Guid messageId, string type, string payload, CancellationToken cancellationToken);
}
