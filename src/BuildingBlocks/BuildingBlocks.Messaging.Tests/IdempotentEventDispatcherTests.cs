using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace BuildingBlocks.Messaging.Tests;

public class IdempotentEventDispatcherTests
{
    private static (IdempotentEventDispatcher Dispatcher, IProcessedEventStore Store) CreateSut()
    {
        var store = Substitute.For<IProcessedEventStore>();
        var dispatcher = new IdempotentEventDispatcher(store, NullLogger<IdempotentEventDispatcher>.Instance);
        return (dispatcher, store);
    }

    [Fact]
    public async Task RunsHandlerAndMarksProcessed_WhenEventIsNew()
    {
        var (dispatcher, store) = CreateSut();
        var eventId = Guid.NewGuid();
        store.IsProcessedAsync(eventId, Arg.Any<CancellationToken>()).Returns(false);
        var handlerRan = false;

        var result = await dispatcher.HandleAsync(eventId, _ =>
        {
            handlerRan = true;
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.True(result);
        Assert.True(handlerRan);
        await store.Received(1).MarkProcessedAsync(eventId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SkipsHandlerAndDoesNotMarkAgain_WhenEventAlreadyProcessed()
    {
        var (dispatcher, store) = CreateSut();
        var eventId = Guid.NewGuid();
        store.IsProcessedAsync(eventId, Arg.Any<CancellationToken>()).Returns(true);
        var handlerRan = false;

        var result = await dispatcher.HandleAsync(eventId, _ =>
        {
            handlerRan = true;
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.False(result);
        Assert.False(handlerRan);
        await store.DidNotReceive().MarkProcessedAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoesNotMarkProcessed_WhenHandlerThrows()
    {
        var (dispatcher, store) = CreateSut();
        var eventId = Guid.NewGuid();
        store.IsProcessedAsync(eventId, Arg.Any<CancellationToken>()).Returns(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.HandleAsync(eventId, _ => throw new InvalidOperationException("handler failed"), CancellationToken.None));

        await store.DidNotReceive().MarkProcessedAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }
}
