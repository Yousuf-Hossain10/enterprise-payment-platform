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
}
