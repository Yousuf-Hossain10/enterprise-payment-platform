using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Wallet.Domain;
using Wallet.Infrastructure;

namespace Wallet.Tests;

/// <summary>
/// Proves the RowVersion (Postgres xmin) optimistic concurrency token on Account
/// actually works against a real, throwaway Postgres container - not just that EF's
/// fluent config compiles. The parallel-debit stress test itself is Day 27's scope;
/// this is the narrower "does the concurrency token detect a stale write at all"
/// check that belongs with the domain model that introduces it.
/// </summary>
public class AccountConcurrencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private string _connectionString = default!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();

        var options = new DbContextOptionsBuilder<WalletDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        await using var db = new WalletDbContext(options);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private WalletDbContext NewContext() =>
        new(new DbContextOptionsBuilder<WalletDbContext>().UseNpgsql(_connectionString).Options);

    [Fact]
    public async Task Concurrent_updates_to_the_same_account_throw_on_the_second_save()
    {
        var accountId = Guid.NewGuid();
        await using (var seedDb = NewContext())
        {
            seedDb.Accounts.Add(new Account
            {
                Id = accountId,
                OwnerId = Guid.NewGuid(),
                Currency = "USD",
                CreatedAtUtc = DateTime.UtcNow
            });
            await seedDb.SaveChangesAsync();
        }

        await using var contextA = NewContext();
        await using var contextB = NewContext();

        var accountA = await contextA.Accounts.SingleAsync(a => a.Id == accountId);
        var accountB = await contextB.Accounts.SingleAsync(a => a.Id == accountId);

        accountA.LastModifiedAtUtc = DateTime.UtcNow;
        contextA.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Amount = 100m,
            Reference = "seed-a",
            IdempotencyKey = Guid.NewGuid().ToString(),
            OccurredAtUtc = DateTime.UtcNow
        });
        await contextA.SaveChangesAsync();

        accountB.LastModifiedAtUtc = DateTime.UtcNow;
        contextB.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Amount = 50m,
            Reference = "seed-b",
            IdempotencyKey = Guid.NewGuid().ToString(),
            OccurredAtUtc = DateTime.UtcNow
        });

        // contextB's tracked Account still carries the pre-contextA-write xmin,
        // so this save must be rejected as a stale write, not silently accepted.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextB.SaveChangesAsync());
    }

    [Fact]
    public async Task Sequential_updates_to_the_same_account_both_succeed()
    {
        var accountId = Guid.NewGuid();
        await using (var seedDb = NewContext())
        {
            seedDb.Accounts.Add(new Account
            {
                Id = accountId,
                OwnerId = Guid.NewGuid(),
                Currency = "USD",
                CreatedAtUtc = DateTime.UtcNow
            });
            await seedDb.SaveChangesAsync();
        }

        await using (var contextA = NewContext())
        {
            var accountA = await contextA.Accounts.SingleAsync(a => a.Id == accountId);
            accountA.LastModifiedAtUtc = DateTime.UtcNow;
            contextA.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                Amount = 100m,
                Reference = "seed-a",
                IdempotencyKey = Guid.NewGuid().ToString(),
                OccurredAtUtc = DateTime.UtcNow
            });
            await contextA.SaveChangesAsync();
        }

        await using var contextB = NewContext();
        var accountB = await contextB.Accounts.SingleAsync(a => a.Id == accountId);
        accountB.LastModifiedAtUtc = DateTime.UtcNow;
        contextB.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Amount = 50m,
            Reference = "seed-b",
            IdempotencyKey = Guid.NewGuid().ToString(),
            OccurredAtUtc = DateTime.UtcNow
        });

        // contextB never loaded the account before contextA committed, so it isn't
        // carrying a stale xmin - this must succeed.
        var exception = await Record.ExceptionAsync(() => contextB.SaveChangesAsync());
        Assert.Null(exception);
    }
}
