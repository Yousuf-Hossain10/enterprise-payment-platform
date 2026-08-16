using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Messaging;

public class RabbitMqOptions
{
    [Required]
    public string HostName { get; set; } = default!;

    public int Port { get; set; } = 5672;

    [Required]
    public string UserName { get; set; } = default!;

    [Required]
    public string Password { get; set; } = default!;

    /// <summary>
    /// Topic exchange every service publishes domain events to. Each consumer
    /// (Notification, Audit) binds its own durable queue with the routing keys
    /// it cares about - see ADR-0001 in docs/adr/.
    /// </summary>
    public string ExchangeName { get; set; } = "payment-platform.events";
}
