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
/// Day 27 wrote this test against a DebitCommandHandler with no retry on
/// ConcurrencyConflictException, which meant the success count landed well short
/// of the account's real capacity (measured: 6 of a possible 10, deterministically,
/// every run - see LedgerWriter's remarks for why). Day 28 added a bounded,
/// jittered retry loop specifically to close that gap; this test's exact-count
/// assertion is what proves it actually closed rather than just "improved" - if the
/// retry logic regresses, this is the test that catches it.
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

        // The hardened behavior this test now locks in: every request the account
        // actually had capacity for should succeed, not just "some of them" - the
        // retry loop's whole job is closing the gap between "raced and lost" and
        // "genuinely out of funds". Measured consistently at 10/10 across 13 runs
        // (8 with this exact test, 5 earlier while tuning) after Day 28's fix;
        // 0/10 or 6/10 (the pre-fix number) would both fail this assertion.
        var theoreticalMaxSuccesses = (int)(openingBalance / debitAmount);
        Assert.Equal(theoreticalMaxSuccesses, successCount);
    }
}
