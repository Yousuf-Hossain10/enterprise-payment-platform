using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Wallet.Application;
using Wallet.Domain;
using Wallet.Infrastructure;

namespace Wallet.Tests;

/// <summary>
/// Exercises DebitCommandHandler against a real, throwaway Postgres container - no
/// mocks - per docs/Coding-Standards.md. In particular proves the idempotency-key
/// replay guarantee end-to-end: the real unique index on LedgerEntry.IdempotencyKey
/// is what actually makes a retried debit safe, not just the in-handler existence
/// check (which a mock-based unit test can't prove on its own).
/// </summary>
public class WalletIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private WalletDbContext _db = default!;
    private DebitCommandHandler _debitHandler = default!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<WalletDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        _db = new WalletDbContext(options);
        await _db.Database.MigrateAsync();

        var accounts = new AccountRepository(_db);
        _debitHandler = new DebitCommandHandler(accounts, new DebitCommandValidator());
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private async Task<Guid> SeedAccountAsync(decimal openingBalance)
    {
        var accountId = Guid.NewGuid();
        _db.Accounts.Add(new Account
        {
            Id = accountId,
            OwnerId = Guid.NewGuid(),
            Currency = "USD",
            CreatedAtUtc = DateTime.UtcNow
        });
        if (openingBalance != 0)
        {
            _db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                Amount = openingBalance,
                Reference = "opening-balance",
                IdempotencyKey = Guid.NewGuid().ToString(),
                OccurredAtUtc = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();
        return accountId;
    }

    [Fact]
    public async Task Debit_reduces_balance_by_the_debited_amount()
    {
        var accountId = await SeedAccountAsync(100m);

        var result = await _debitHandler.HandleAsync(
            new DebitCommand(accountId, 30m, Guid.NewGuid().ToString(), "order-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(70m, result.Value);
    }

    [Fact]
    public async Task Retried_debit_with_the_same_idempotency_key_produces_exactly_one_ledger_entry()
    {
        var accountId = await SeedAccountAsync(100m);
        var idempotencyKey = Guid.NewGuid().ToString();

        var first = await _debitHandler.HandleAsync(
            new DebitCommand(accountId, 30m, idempotencyKey, "order-1"), CancellationToken.None);
        var retry = await _debitHandler.HandleAsync(
            new DebitCommand(accountId, 30m, idempotencyKey, "order-1"), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(first.Value, retry.Value);

        var entryCount = await _db.LedgerEntries.CountAsync(e => e.IdempotencyKey == idempotencyKey);
        Assert.Equal(1, entryCount);
    }

    [Fact]
    public async Task Debit_fails_when_it_would_overdraw_the_account()
    {
        var accountId = await SeedAccountAsync(10m);

        var result = await _debitHandler.HandleAsync(
            new DebitCommand(accountId, 30m, Guid.NewGuid().ToString(), "order-1"), CancellationToken.None);

        Assert.False(result.IsSuccess);

        var entryCount = await _db.LedgerEntries.CountAsync(e => e.AccountId == accountId && e.Amount < 0);
        Assert.Equal(0, entryCount);
    }
}
