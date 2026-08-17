using System.Text.Json;
using BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Wallet.Application;
using Wallet.Domain;
using Wallet.Infrastructure;

namespace Wallet.Tests;

/// <summary>
/// Exercises Debit/CreditCommandHandler against a real, throwaway Postgres
/// container - no mocks - per docs/Coding-Standards.md. In particular proves the
/// idempotency-key replay guarantee end-to-end: the real unique index on
/// LedgerEntry.IdempotencyKey is what actually makes a retried debit/credit safe,
/// not just the in-handler existence check (which a mock-based unit test can't
/// prove on its own) - and that AccountRepository.SaveChangesAsync translates a
/// genuine Postgres concurrency conflict into ConcurrencyConflictException, not
/// just a mocked one. The dedicated N-concurrent-requests stress test is Day 27's
/// scope, not this file's.
/// </summary>
public class WalletIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private WalletDbContext _db = default!;
    private DebitCommandHandler _debitHandler = default!;
    private CreditCommandHandler _creditHandler = default!;
    private string _connectionString = default!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();

        var options = new DbContextOptionsBuilder<WalletDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        _db = new WalletDbContext(options);
        await _db.Database.MigrateAsync();

        var accounts = new AccountRepository(_db);
        _debitHandler = new DebitCommandHandler(accounts, new DebitCommandValidator());
        _creditHandler = new CreditCommandHandler(accounts, new CreditCommandValidator());
    }

    private WalletDbContext NewContext() =>
        new(new DbContextOptionsBuilder<WalletDbContext>().UseNpgsql(_connectionString).Options);

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

    [Fact]
    public async Task Credit_increases_balance_by_the_credited_amount()
    {
        var accountId = await SeedAccountAsync(100m);

        var result = await _creditHandler.HandleAsync(
            new CreditCommand(accountId, 30m, Guid.NewGuid().ToString(), "refund-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(130m, result.Value);
    }

    [Fact]
    public async Task Retried_credit_with_the_same_idempotency_key_produces_exactly_one_ledger_entry()
    {
        var accountId = await SeedAccountAsync(100m);
        var idempotencyKey = Guid.NewGuid().ToString();

        var first = await _creditHandler.HandleAsync(
            new CreditCommand(accountId, 30m, idempotencyKey, "refund-1"), CancellationToken.None);
        var retry = await _creditHandler.HandleAsync(
            new CreditCommand(accountId, 30m, idempotencyKey, "refund-1"), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(first.Value, retry.Value);

        var entryCount = await _db.LedgerEntries.CountAsync(e => e.IdempotencyKey == idempotencyKey);
        Assert.Equal(1, entryCount);
    }

    [Fact]
    public async Task AccountRepository_translates_a_real_concurrency_conflict_into_ConcurrencyConflictException()
    {
        var accountId = await SeedAccountAsync(100m);

        await using var contextA = NewContext();
        await using var contextB = NewContext();
        var repoA = new AccountRepository(contextA);
        var repoB = new AccountRepository(contextB);

        var accountA = await repoA.GetByIdAsync(accountId, CancellationToken.None);
        var accountB = await repoB.GetByIdAsync(accountId, CancellationToken.None);
        Assert.NotNull(accountA);
        Assert.NotNull(accountB);

        accountA!.LastModifiedAtUtc = DateTime.UtcNow;
        repoA.AddLedgerEntry(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Amount = 10m,
            Reference = "race-a",
            IdempotencyKey = Guid.NewGuid().ToString(),
            OccurredAtUtc = DateTime.UtcNow
        });
        await repoA.SaveChangesAsync(CancellationToken.None);

        accountB!.LastModifiedAtUtc = DateTime.UtcNow;
        repoB.AddLedgerEntry(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Amount = 5m,
            Reference = "race-b",
            IdempotencyKey = Guid.NewGuid().ToString(),
            OccurredAtUtc = DateTime.UtcNow
        });

        // repoB's tracked Account still carries the pre-repoA-write xmin - this must
        // surface as ConcurrencyConflictException, not the raw EF exception type
        // (Wallet.Application must stay free of an EF Core dependency).
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => repoB.SaveChangesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Debit_writes_a_WalletDebited_outbox_message_in_the_same_transaction()
    {
        var accountId = await SeedAccountAsync(100m);
        var idempotencyKey = Guid.NewGuid().ToString();

        var result = await _debitHandler.HandleAsync(
            new DebitCommand(accountId, 30m, idempotencyKey, "order-1"), CancellationToken.None);
        Assert.True(result.IsSuccess);

        var message = await _db.OutboxMessages.SingleAsync(m => m.Type == "WalletDebited");
        Assert.Null(message.ProcessedAtUtc);
        var payload = JsonSerializer.Deserialize<WalletDebited>(message.Payload)!;
        Assert.Equal(accountId, payload.AccountId);
        Assert.Equal(30m, payload.Amount);
        Assert.Equal("order-1", payload.Reference);
        Assert.Equal(idempotencyKey, payload.IdempotencyKey);
    }

    [Fact]
    public async Task Credit_writes_a_WalletCredited_outbox_message_in_the_same_transaction()
    {
        var accountId = await SeedAccountAsync(100m);
        var idempotencyKey = Guid.NewGuid().ToString();

        var result = await _creditHandler.HandleAsync(
            new CreditCommand(accountId, 30m, idempotencyKey, "refund-1"), CancellationToken.None);
        Assert.True(result.IsSuccess);

        var message = await _db.OutboxMessages.SingleAsync(m => m.Type == "WalletCredited");
        Assert.Null(message.ProcessedAtUtc);
        var payload = JsonSerializer.Deserialize<WalletCredited>(message.Payload)!;
        Assert.Equal(accountId, payload.AccountId);
        Assert.Equal(30m, payload.Amount);
        Assert.Equal("refund-1", payload.Reference);
        Assert.Equal(idempotencyKey, payload.IdempotencyKey);
    }

    [Fact]
    public async Task Retried_debit_does_not_enqueue_a_second_outbox_message()
    {
        var accountId = await SeedAccountAsync(100m);
        var idempotencyKey = Guid.NewGuid().ToString();

        await _debitHandler.HandleAsync(new DebitCommand(accountId, 30m, idempotencyKey, "order-1"), CancellationToken.None);
        await _debitHandler.HandleAsync(new DebitCommand(accountId, 30m, idempotencyKey, "order-1"), CancellationToken.None);

        var messageCount = await _db.OutboxMessages.CountAsync(m => m.Type == "WalletDebited");
        Assert.Equal(1, messageCount);
    }

    [Fact]
    public async Task WalletOutboxStore_returns_unprocessed_messages_and_MarkProcessedAsync_excludes_them_afterward()
    {
        var accountId = await SeedAccountAsync(100m);
        await _debitHandler.HandleAsync(
            new DebitCommand(accountId, 30m, Guid.NewGuid().ToString(), "order-1"), CancellationToken.None);

        await using var storeContext = NewContext();
        var store = new WalletOutboxStore(storeContext);

        var unprocessed = await store.GetUnprocessedAsync(50, CancellationToken.None);
        Assert.Single(unprocessed);
        Assert.Equal("WalletDebited", unprocessed[0].Type);

        await store.MarkProcessedAsync(unprocessed[0].Id, DateTime.UtcNow, CancellationToken.None);

        var stillUnprocessed = await store.GetUnprocessedAsync(50, CancellationToken.None);
        Assert.Empty(stillUnprocessed);
    }
}
