using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Testcontainers.RabbitMq;

namespace BuildingBlocks.Messaging.Tests;

/// <summary>
/// Exercises RabbitMqMessagePublisher and RabbitMqConsumerBackgroundService
/// together against a real, throwaway RabbitMQ broker - no mocks - per
/// docs/Coding-Standards.md. In particular proves the Day 39 fix: before this,
/// IMessagePublisher.PublishAsync never set the AMQP MessageId property, leaving a
/// consumer with no stable identifier to key an idempotent-consumer check on at
/// all. This test proves that identifier now actually round-trips from publish to
/// delivery, not just that the code compiles.
/// </summary>
public class RabbitMqConsumerIntegrationTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:3.13-management-alpine").Build();

    public async Task InitializeAsync() => await _rabbitMq.StartAsync();

    public async Task DisposeAsync() => await _rabbitMq.DisposeAsync();

    private class RecordingEventHandler : IEventHandler
    {
        public ConcurrentBag<(Guid MessageId, string Type, string Payload)> Received { get; } = [];

        public Task HandleAsync(Guid messageId, string type, string payload, CancellationToken cancellationToken)
        {
            Received.Add((messageId, type, payload));
            return Task.CompletedTask;
        }
    }

    private IOptions<RabbitMqOptions> BuildRabbitMqOptions()
    {
        // RabbitMqBuilder generates random credentials by default (not guest/guest) -
        // the connection string carries them, e.g. amqp://user:pass@host:port/.
        var uri = new Uri(_rabbitMq.GetConnectionString());
        var userInfo = uri.UserInfo.Split(':', 2);
        return Options.Create(new RabbitMqOptions
        {
            HostName = uri.Host,
            Port = uri.Port,
            UserName = Uri.UnescapeDataString(userInfo[0]),
            Password = Uri.UnescapeDataString(userInfo[1])
        });
    }

    [Fact]
    public async Task Consumer_receives_a_published_message_with_the_correct_messageId_type_and_payload()
    {
        var rabbitMqOptions = BuildRabbitMqOptions();
        await using var publisher = new RabbitMqMessagePublisher(rabbitMqOptions);

        var handler = new RecordingEventHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IEventHandler>(handler);
        await using var provider = services.BuildServiceProvider();

        var consumerOptions = Options.Create(new RabbitMqConsumerOptions
        {
            QueueName = $"test-queue-{Guid.NewGuid():N}",
            RoutingKeys = ["TestEvent"]
        });

        var consumer = new RabbitMqConsumerBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            rabbitMqOptions,
            consumerOptions,
            NullLogger<RabbitMqConsumerBackgroundService>.Instance);

        await consumer.StartAsync(CancellationToken.None);
        // BackgroundService.StartAsync returns once ExecuteAsync begins running, not
        // once it finishes declaring/binding the queue and registering the consumer -
        // give that setup a moment to actually complete before publishing, or the
        // message has nothing bound to land on yet and is simply dropped.
        await Task.Delay(1000);
        try
        {
            var messageId = Guid.NewGuid();
            await publisher.PublishAsync(messageId, "TestEvent", "{\"hello\":\"world\"}", CancellationToken.None);

            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (handler.Received.IsEmpty && DateTime.UtcNow < deadline)
                await Task.Delay(100);

            var received = Assert.Single(handler.Received);
            Assert.Equal(messageId, received.MessageId);
            Assert.Equal("TestEvent", received.Type);
            Assert.Equal("{\"hello\":\"world\"}", received.Payload);
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Consumer_only_receives_messages_matching_its_bound_routing_keys()
    {
        var rabbitMqOptions = BuildRabbitMqOptions();
        await using var publisher = new RabbitMqMessagePublisher(rabbitMqOptions);

        var handler = new RecordingEventHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IEventHandler>(handler);
        await using var provider = services.BuildServiceProvider();

        var consumerOptions = Options.Create(new RabbitMqConsumerOptions
        {
            QueueName = $"test-queue-{Guid.NewGuid():N}",
            RoutingKeys = ["WantedEvent"]
        });

        var consumer = new RabbitMqConsumerBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            rabbitMqOptions,
            consumerOptions,
            NullLogger<RabbitMqConsumerBackgroundService>.Instance);

        await consumer.StartAsync(CancellationToken.None);
        // BackgroundService.StartAsync returns once ExecuteAsync begins running, not
        // once it finishes declaring/binding the queue and registering the consumer -
        // give that setup a moment to actually complete before publishing, or the
        // message has nothing bound to land on yet and is simply dropped.
        await Task.Delay(1000);
        try
        {
            await publisher.PublishAsync(Guid.NewGuid(), "UnwantedEvent", "{}", CancellationToken.None);
            var wantedId = Guid.NewGuid();
            await publisher.PublishAsync(wantedId, "WantedEvent", "{}", CancellationToken.None);

            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (handler.Received.IsEmpty && DateTime.UtcNow < deadline)
                await Task.Delay(100);

            // Give any (incorrect) delivery of the unwanted event a moment to
            // arrive too, so this isn't just a timing-lucky pass.
            await Task.Delay(500);

            var received = Assert.Single(handler.Received);
            Assert.Equal(wantedId, received.MessageId);
            Assert.Equal("WantedEvent", received.Type);
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }
}
