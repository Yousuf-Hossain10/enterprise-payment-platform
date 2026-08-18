using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Per-service consumer configuration - which durable queue to declare and which
/// routing keys (event type names, since RabbitMqMessagePublisher routes on Type)
/// to bind it to on the shared topic exchange (RabbitMqOptions.ExchangeName).
/// </summary>
public class RabbitMqConsumerOptions
{
    [Required]
    public string QueueName { get; set; } = default!;

    [MinLength(1)]
    public string[] RoutingKeys { get; set; } = [];
}
