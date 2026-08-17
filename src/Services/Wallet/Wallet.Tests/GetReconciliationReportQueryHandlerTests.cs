using NSubstitute;
using Wallet.Application;

namespace Wallet.Tests;

public class GetReconciliationReportQueryHandlerTests
{
    private static GetReconciliationReportQueryHandler CreateSut(out IReconciliationRepository reconciliation)
    {
        reconciliation = Substitute.For<IReconciliationRepository>();
        return new GetReconciliationReportQueryHandler(reconciliation);
    }

    [Fact]
    public async Task Succeeds_AndReportsOneEntryPerAccount_WhenAllAccountsReconcile()
    {
        var accountA = Guid.NewGuid();
        var accountB = Guid.NewGuid();
        var sut = CreateSut(out var reconciliation);
        reconciliation.GetAllAccountIdsAsync(Arg.Any<CancellationToken>()).Returns([accountA, accountB]);
        reconciliation.GetAccountLedgerSumsAsync(accountA, Arg.Any<CancellationToken>())
            .Returns(new AccountLedgerSums(100m, 100m, 3));
        reconciliation.GetAccountLedgerSumsAsync(accountB, Arg.Any<CancellationToken>())
            .Returns(new AccountLedgerSums(250m, 250m, 5));

        var result = await sut.HandleAsync(new GetReconciliationReportQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Accounts.Count);
        Assert.True(result.Value.AllReconciled);
    }

    [Fact]
    public async Task AllReconciled_IsFalse_WhenAnyAccountsLinqAndRawSqlSumsDisagree()
    {
        var accountA = Guid.NewGuid();
        var accountB = Guid.NewGuid();
        var sut = CreateSut(out var reconciliation);
        reconciliation.GetAllAccountIdsAsync(Arg.Any<CancellationToken>()).Returns([accountA, accountB]);
        reconciliation.GetAccountLedgerSumsAsync(accountA, Arg.Any<CancellationToken>())
            .Returns(new AccountLedgerSums(100m, 100m, 3));
        // Simulates the P1 bug scenario: the two independent computation paths disagree.
        reconciliation.GetAccountLedgerSumsAsync(accountB, Arg.Any<CancellationToken>())
            .Returns(new AccountLedgerSums(250m, 249.99m, 5));

        var result = await sut.HandleAsync(new GetReconciliationReportQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.AllReconciled);
        Assert.True(result.Value.Accounts.Single(a => a.AccountId == accountA).IsReconciled);
        Assert.False(result.Value.Accounts.Single(a => a.AccountId == accountB).IsReconciled);
    }

    [Fact]
    public async Task Succeeds_WithAnEmptyReport_WhenThereAreNoAccounts()
    {
        var sut = CreateSut(out var reconciliation);
        reconciliation.GetAllAccountIdsAsync(Arg.Any<CancellationToken>()).Returns([]);

        var result = await sut.HandleAsync(new GetReconciliationReportQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Accounts);
        Assert.True(result.Value.AllReconciled);
    }
}
