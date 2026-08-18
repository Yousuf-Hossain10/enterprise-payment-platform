using BuildingBlocks.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BuildingBlocks.Messaging;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the RabbitMQ publisher and the outbox dispatcher background
    /// service. Callers must separately register their own <see cref="IOutboxStore"/>
    /// implementation against their service's DbContext - this library has no
    /// opinion on which database or ORM a service uses.
    /// </summary>
    public static IServiceCollection AddOutboxDispatcher(this IServiceCollection services)
    {
        services.AddValidatedOptions<RabbitMqOptions>("RabbitMq");
        services.AddValidatedOptions<OutboxDispatcherOptions>("OutboxDispatcher");

        services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();
        services.AddHostedService<OutboxDispatcherBackgroundService>();

        return services;
    }

    /// <summary>
    /// Registers <see cref="IdempotentEventDispatcher"/>. Callers must separately
    /// register their own <see cref="IProcessedEventStore"/> implementation against
    /// their service's DbContext.
    /// </summary>
    public static IServiceCollection AddIdempotentEventConsumer(this IServiceCollection services)
    {
        services.AddScoped<IdempotentEventDispatcher>();
        return services;
    }

    /// <summary>
    /// Registers the RabbitMQ consumer background service, bound to the routing
    /// keys configured under "RabbitMqConsumer". Callers must separately register
    /// their own <see cref="IEventHandler"/> implementation - this library has no
    /// opinion on what a delivered message means. Safe to call alongside
    /// <see cref="AddOutboxDispatcher"/> in a service that both publishes and
    /// consumes (re-registering the shared "RabbitMq" options section twice is
    /// harmless - it just runs the same validation twice).
    /// </summary>
    public static IServiceCollection AddRabbitMqConsumer(this IServiceCollection services)
    {
        services.AddValidatedOptions<RabbitMqOptions>("RabbitMq");
        services.AddValidatedOptions<RabbitMqConsumerOptions>("RabbitMqConsumer");

        services.AddHostedService<RabbitMqConsumerBackgroundService>();

        return services;
    }
}
