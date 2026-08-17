using NSubstitute;
using Wallet.Application;
using Wallet.Domain;

namespace Wallet.Tests;

public class CreditCommandHandlerTests
{
    private static CreditCommandHandler CreateSut(out IAccountRepository accounts)
    {
        accounts = Substitute.For<IAccountRepository>();
        return new CreditCommandHandler(accounts, new CreditCommandValidator());
    }

    private static Account SampleAccount(Guid id) =>
        new() { Id = id, OwnerId = Guid.NewGuid(), Currency = "USD", CreatedAtUtc = DateTime.UtcNow };

    [Fact]
    public async Task Succeeds_AndAddsLedgerEntry()
    {
        var accountId = Guid.NewGuid();
        var sut = CreateSut(out var accounts);
        accounts.ExistsByIdempotencyKeyAsync("key-1", Arg.Any<CancellationToken>()).Returns(false);
        accounts.GetByIdAsync(accountId, Arg.Any<CancellationToken>()).Returns(SampleAccount(accountId));
        accounts.GetBalanceAsync(accountId, Arg.Any<CancellationToken>()).Returns(100m);

        var result = await sut.HandleAsync(
            new CreditCommand(accountId, 40m, "key-1", "ref-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(140m, result.Value);
        accounts.Received(1).AddLedgerEntry(Arg.Is<LedgerEntry>(e =>
            e.AccountId == accountId &&
            e.Amount == 40m &&
            e.Reference == "ref-1" &&
            e.IdempotencyKey == "key-1"));
        accounts.Received(1).EnqueueEvent("WalletCredited", Arg.Is<WalletCredited>(e =>
            e.AccountId == accountId &&
            e.Amount == 40m &&
            e.Reference == "ref-1" &&
            e.IdempotencyKey == "key-1"));
        await accounts.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Succeeds_EvenWithZeroStartingBalance_NoUpperBoundCheck()
    {
        var accountId = Guid.NewGuid();
        var sut = CreateSut(out var accounts);
        accounts.ExistsByIdempotencyKeyAsync("key-1", Arg.Any<CancellationToken>()).Returns(false);
        accounts.GetByIdAsync(accountId, Arg.Any<CancellationToken>()).Returns(SampleAccount(accountId));
        accounts.GetBalanceAsync(accountId, Arg.Any<CancellationToken>()).Returns(0m);

        var result = await sut.HandleAsync(
            new CreditCommand(accountId, 1_000_000m, "key-1", "ref-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1_000_000m, result.Value);
    }

    [Fact]
    public async Task IsIdempotent_AndReturnsCurrentBalance_WithoutAddingASecondEntry_OnRetry()
    {
        var accountId = Guid.NewGuid();
        var sut = CreateSut(out var accounts);
        accounts.ExistsByIdempotencyKeyAsync("key-1", Arg.Any<CancellationToken>()).Returns(true);
        accounts.GetBalanceAsync(accountId, Arg.Any<CancellationToken>()).Returns(140m);

        var result = await sut.HandleAsync(
            new CreditCommand(accountId, 40m, "key-1", "ref-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(140m, result.Value);
        accounts.DidNotReceiveWithAnyArgs().AddLedgerEntry(default!);
        await accounts.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Fails_WhenAccountDoesNotExist()
    {
        var accountId = Guid.NewGuid();
        var sut = CreateSut(out var accounts);
        accounts.ExistsByIdempotencyKeyAsync("key-1", Arg.Any<CancellationToken>()).Returns(false);
        accounts.GetByIdAsync(accountId, Arg.Any<CancellationToken>()).Returns((Account?)null);

        var result = await sut.HandleAsync(
            new CreditCommand(accountId, 40m, "key-1", "ref-1"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        accounts.DidNotReceiveWithAnyArgs().AddLedgerEntry(default!);
        accounts.DidNotReceiveWithAnyArgs().EnqueueEvent(default!, default!);
    }

    [Fact]
    public async Task Fails_WhenConcurrentModificationIsDetected_OnEveryAttempt()
    {
        var accountId = Guid.NewGuid();
        var sut = CreateSut(out var accounts);
        accounts.ExistsByIdempotencyKeyAsync("key-1", Arg.Any<CancellationToken>()).Returns(false);
        accounts.GetByIdAsync(accountId, Arg.Any<CancellationToken>()).Returns(SampleAccount(accountId));
        accounts.GetBalanceAsync(accountId, Arg.Any<CancellationToken>()).Returns(100m);
        accounts.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new ConcurrencyConflictException("stale", new Exception()));

        var result = await sut.HandleAsync(
            new CreditCommand(accountId, 40m, "key-1", "ref-1"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        await accounts.Received(25).SaveChangesAsync(Arg.Any<CancellationToken>());
        await accounts.Received(24).ReloadAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Succeeds_AfterRetryingPastATransientConcurrencyConflict()
    {
        var accountId = Guid.NewGuid();
        var sut = CreateSut(out var accounts);
        accounts.ExistsByIdempotencyKeyAsync("key-1", Arg.Any<CancellationToken>()).Returns(false);
        accounts.GetByIdAsync(accountId, Arg.Any<CancellationToken>()).Returns(SampleAccount(accountId));
        accounts.GetBalanceAsync(accountId, Arg.Any<CancellationToken>()).Returns(100m);

        var saveAttempt = 0;
        accounts.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            saveAttempt++;
            return saveAttempt == 1
                ? throw new ConcurrencyConflictException("stale", new Exception())
                : Task.CompletedTask;
        });

        var result = await sut.HandleAsync(
            new CreditCommand(accountId, 40m, "key-1", "ref-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(140m, result.Value);
        await accounts.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
        await accounts.Received(1).ReloadAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Fails_ForNonPositiveAmount(decimal amount)
    {
        var sut = CreateSut(out _);

        var result = await sut.HandleAsync(
            new CreditCommand(Guid.NewGuid(), amount, "key-1", "ref-1"), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
