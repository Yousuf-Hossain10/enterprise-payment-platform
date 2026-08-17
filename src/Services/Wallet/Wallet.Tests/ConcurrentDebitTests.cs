using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Wallet.Application;
using Wallet.Domain;
using Wallet.Infrastructure;

namespace Wallet.Tests;

/// <summary>
/// The single most important test in the platform (docs/Enterprise_Payment_Platform_Tutorial.md,
/// Phase 6): fires many genuinely concurrent debits against the same account -
/// each on its own DbContext/AccountRepository, since DbContext isn't thread-safe -
/// and proves the ledger can never be overdrawn no matter how the requests race.
///
/// DebitCommandHandler does not currently retry on ConcurrencyConflictException -
/// it fails closed and returns the failure to the caller. That's deliberate for
/// this test: it means some of the N concurrent requests below are expected to
/// fail with a transient "Concurrent modification - retry" result rather than
/// "Insufficient funds", even though funds were sufficient at the moment they were
/// fired - a caller would retry those. Whether the success count should instead
/// land closer to floor(openingBalance / debitAmount) via an in-handler retry loop
/// is exactly what Day 28 ("harden edge cases the concurrency test surfaces") is
/// for - not solved here. What this test asserts unconditionally, regardless of
/// retry behavior: the ledger is never overdrawn, and every successful debit
/// produced exactly one ledger entry - no double-processing, no lost writes.
/// </summary>
public class ConcurrentDebitTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private string _connectionString = default!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();

        var options = new DbContextOptionsBuilder<WalletDbContext>().UseNpgsql(_connectionString).Options;
        await using var db = new WalletDbContext(options);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private WalletDbContext NewContext() =>
        new(new DbContextOptionsBuilder<WalletDbContext>().UseNpgsql(_connectionString).Options);

    [Fact]
    public async Task Twenty_concurrent_debits_never_overdraw_the_account()
    {
        const decimal openingBalance = 1_000m;
        const decimal debitAmount = 100m;
        const int concurrentRequests = 20;

        var accountId = Guid.NewGuid();
        await using (var seedDb = NewContext())
        {
            seedDb.Accounts.Add(new Domain.Account
            {
                Id = accountId,
                OwnerId = Guid.NewGuid(),
                Currency = "USD",
                CreatedAtUtc = DateTime.UtcNow
            });
            seedDb.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                Amount = openingBalance,
                Reference = "opening-balance",
                IdempotencyKey = Guid.NewGuid().ToString(),
                OccurredAtUtc = DateTime.UtcNow
            });
            await seedDb.SaveChangesAsync();
        }

        // Each task gets its own DbContext/AccountRepository/handler (and its own
        // idempotency key) so this is N independent debit attempts racing for real,
        // not N retries of one request.
        var tasks = Enumerable.Range(0, concurrentRequests).Select(async i =>
        {
            await using var context = NewContext();
            var accounts = new AccountRepository(context);
            var handler = new DebitCommandHandler(accounts, new DebitCommandValidator());
            return await handler.HandleAsync(
                new DebitCommand(accountId, debitAmount, $"concurrent-key-{i}", $"concurrent-ref-{i}"),
                CancellationToken.None);
        });

        var results = await Task.WhenAll(tasks);

        var successCount = results.Count(r => r.IsSuccess);

        await using var verifyDb = NewContext();
        var debitEntryCount = await verifyDb.LedgerEntries.CountAsync(e => e.AccountId == accountId && e.Amount < 0);
        var finalBalance = await verifyDb.LedgerEntries
            .Where(e => e.AccountId == accountId)
            .SumAsync(e => e.Amount);

        // The non-negotiable safety invariant: the ledger can never be overdrawn,
        // no matter how many requests raced for it.
        Assert.True(finalBalance >= 0, $"Account was overdrawn: final balance {finalBalance}.");

        // Every result the handler reported successful must correspond to exactly
        // one ledger entry - no request that got a success Result silently failed
        // to persist, and no entry exists that wasn't reported as a success.
        Assert.Equal(successCount, debitEntryCount);

        // The math must reconcile exactly: opening balance minus however many
        // debits actually landed.
        Assert.Equal(openingBalance - (successCount * debitAmount), finalBalance);

        // Sanity check that the test actually exercised real contention rather
        // than trivially succeeding or trivially failing every request.
        Assert.InRange(successCount, 1, concurrentRequests);
    }
}
