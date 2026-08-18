using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BuildingBlocks.Messaging.Tests;

public class OutboxDispatcherBackgroundServiceTests
{
    private static (OutboxDispatcherBackgroundService Dispatcher, IOutboxStore Store, IMessagePublisher Publisher) CreateSut(
        int batchSize = 50, TimeSpan? pollInterval = null)
    {
        var store = Substitute.For<IOutboxStore>();
        var publisher = Substitute.For<IMessagePublisher>();

        var services = new ServiceCollection();
        services.AddScoped(_ => store);
        var provider = services.BuildServiceProvider();

        var options = Options.Create(new OutboxDispatcherOptions
        {
            BatchSize = batchSize,
            PollInterval = pollInterval ?? TimeSpan.FromSeconds(1)
        });

        var dispatcher = new OutboxDispatcherBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            publisher,
            options,
            NullLogger<OutboxDispatcherBackgroundService>.Instance);

        return (dispatcher, store, publisher);
    }

    private static OutboxMessage NewMessage(string type) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        Payload = "{}",
        OccurredAtUtc = DateTime.UtcNow
    };

    [Fact]
    public async Task PublishesAndMarksEachUnprocessedMessage()
    {
        var (dispatcher, store, publisher) = CreateSut();
        var message = NewMessage("TestEvent");
        store.GetUnprocessedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([message]);

        await dispatcher.DispatchBatchAsync(CancellationToken.None);

        await publisher.Received(1).PublishAsync(message.Id, "TestEvent", "{}", Arg.Any<CancellationToken>());
        await store.Received(1).MarkProcessedAsync(message.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LeavesMessageUnprocessed_WhenPublishThrows()
    {
        var (dispatcher, store, publisher) = CreateSut();
        var message = NewMessage("TestEvent");
        store.GetUnprocessedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([message]);
        publisher.When(p => p.PublishAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("broker down"));

        await dispatcher.DispatchBatchAsync(CancellationToken.None);

        await store.DidNotReceive().MarkProcessedAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ContinuesToNextMessage_WhenOneFails()
    {
        var (dispatcher, store, publisher) = CreateSut();
        var failing = NewMessage("Failing");
        var succeeding = NewMessage("Succeeding");
        store.GetUnprocessedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([failing, succeeding]);
        publisher.When(p => p.PublishAsync(failing.Id, "Failing", Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("broker down"));

        await dispatcher.DispatchBatchAsync(CancellationToken.None);

        await store.Received(1).MarkProcessedAsync(succeeding.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await store.DidNotReceive().MarkProcessedAsync(failing.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoOp_WhenNoUnprocessedMessages()
    {
        var (dispatcher, store, publisher) = CreateSut();
        store.GetUnprocessedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await dispatcher.DispatchBatchAsync(CancellationToken.None);

        await publisher.DidNotReceiveWithAnyArgs().PublishAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task DispatchBatchAsync_RequestsConfiguredBatchSize()
    {
        var (dispatcher, store, _) = CreateSut(batchSize: 7);
        store.GetUnprocessedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await dispatcher.DispatchBatchAsync(CancellationToken.None);

        await store.Received(1).GetUnprocessedAsync(7, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PollsRepeatedly_UntilStopped()
    {
        var (dispatcher, store, _) = CreateSut(pollInterval: TimeSpan.FromMilliseconds(30));
        var callCount = 0;
        store.GetUnprocessedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OutboxMessage>>([]))
            .AndDoes(_ => Interlocked.Increment(ref callCount));

        await dispatcher.StartAsync(CancellationToken.None);
        await Task.Delay(160); // ~5 poll intervals at 30ms
        await dispatcher.StopAsync(CancellationToken.None);

        Assert.True(callCount >= 3, $"Expected at least 3 poll cycles, got {callCount}.");
    }
}
