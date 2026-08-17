using BuildingBlocks.Common;

namespace Wallet.Application;

public record GetReconciliationReportQuery;

public record AccountReconciliation(Guid AccountId, decimal LedgerSum, decimal RawSqlLedgerSum, int LedgerEntryCount)
{
    /// <summary>True unless the two independent computation paths disagree - see
    /// IReconciliationRepository.GetAccountLedgerSumsAsync for why that's a P1 bug.</summary>
    public bool IsReconciled => LedgerSum == RawSqlLedgerSum;
}

public record ReconciliationReport(IReadOnlyList<AccountReconciliation> Accounts)
{
    public bool AllReconciled => Accounts.All(a => a.IsReconciled);
}

public class GetReconciliationReportQueryHandler
{
    private readonly IReconciliationRepository _reconciliation;

    public GetReconciliationReportQueryHandler(IReconciliationRepository reconciliation)
    {
        _reconciliation = reconciliation;
    }

    public async Task<Result<ReconciliationReport>> HandleAsync(
        GetReconciliationReportQuery query, CancellationToken cancellationToken)
    {
        var accountIds = await _reconciliation.GetAllAccountIdsAsync(cancellationToken);

        var accounts = new List<AccountReconciliation>(accountIds.Count);
        foreach (var accountId in accountIds)
        {
            var sums = await _reconciliation.GetAccountLedgerSumsAsync(accountId, cancellationToken);
            accounts.Add(new AccountReconciliation(accountId, sums.LinqSum, sums.RawSqlSum, sums.LedgerEntryCount));
        }

        return Result<ReconciliationReport>.Success(new ReconciliationReport(accounts));
    }
}
