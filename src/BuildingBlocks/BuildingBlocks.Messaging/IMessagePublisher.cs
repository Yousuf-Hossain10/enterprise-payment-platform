namespace BuildingBlocks.Messaging;

public interface IMessagePublisher
{
    Task PublishAsync(string type, string payload, CancellationToken cancellationToken);
}
