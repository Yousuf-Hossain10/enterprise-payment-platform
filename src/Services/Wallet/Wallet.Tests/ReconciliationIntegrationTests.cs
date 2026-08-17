using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Wallet.Application;
using Wallet.Domain;
using Wallet.Infrastructure;

namespace Wallet.Tests;

/// <summary>
/// Proves the reconciliation report's raw-SQL ground-truth path actually agrees
/// with the LINQ-computed balance against a real Postgres instance - the whole
/// point of the cross-check is catching an ORM-translation bug, which a mock can't
/// exercise since a mock never translates anything.
/// </summary>
public class ReconciliationIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private WalletDbContext _db = default!;
    private DebitCommandHandler _debitHandler = default!;
    private CreditCommandHandler _creditHandler = default!;
    private GetReconciliationReportQueryHandler _reconciliationHandler = default!;

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
        _creditHandler = new CreditCommandHandler(accounts, new CreditCommandValidator());
        _reconciliationHandler = new GetReconciliationReportQueryHandler(new ReconciliationRepository(_db));
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private async Task<Guid> SeedAccountAsync()
    {
        var accountId = Guid.NewGuid();
        _db.Accounts.Add(new Account
        {
            Id = accountId,
            OwnerId = Guid.NewGuid(),
            Currency = "USD",
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return accountId;
    }

    [Fact]
    public async Task Report_covers_every_account_and_all_reconcile_after_real_debits_and_credits()
    {
        var accountA = await SeedAccountAsync();
        var accountB = await SeedAccountAsync();

        await _creditHandler.HandleAsync(
            new CreditCommand(accountA, 500m, Guid.NewGuid().ToString(), "opening"), CancellationToken.None);
        await _debitHandler.HandleAsync(
            new DebitCommand(accountA, 120m, Guid.NewGuid().ToString(), "order-1"), CancellationToken.None);
        await _creditHandler.HandleAsync(
            new CreditCommand(accountB, 75.50m, Guid.NewGuid().ToString(), "opening"), CancellationToken.None);

        var result = await _reconciliationHandler.HandleAsync(new GetReconciliationReportQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var report = result.Value!;
        Assert.True(report.AllReconciled);

        var accountAReport = report.Accounts.Single(a => a.AccountId == accountA);
        Assert.Equal(380m, accountAReport.LedgerSum);
        Assert.Equal(380m, accountAReport.RawSqlLedgerSum);
        Assert.Equal(2, accountAReport.LedgerEntryCount);

        var accountBReport = report.Accounts.Single(a => a.AccountId == accountB);
        Assert.Equal(75.50m, accountBReport.LedgerSum);
        Assert.Equal(75.50m, accountBReport.RawSqlLedgerSum);
        Assert.Equal(1, accountBReport.LedgerEntryCount);
    }

    [Fact]
    public async Task Report_includes_accounts_with_no_ledger_entries_at_zero_balance()
    {
        var accountId = await SeedAccountAsync();

        var result = await _reconciliationHandler.HandleAsync(new GetReconciliationReportQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var accountReport = result.Value!.Accounts.Single(a => a.AccountId == accountId);
        Assert.Equal(0m, accountReport.LedgerSum);
        Assert.Equal(0m, accountReport.RawSqlLedgerSum);
        Assert.Equal(0, accountReport.LedgerEntryCount);
        Assert.True(accountReport.IsReconciled);
    }
}
