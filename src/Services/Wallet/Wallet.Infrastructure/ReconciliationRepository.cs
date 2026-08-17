using Microsoft.EntityFrameworkCore;
using Wallet.Application;

namespace Wallet.Infrastructure;

public class ReconciliationRepository : IReconciliationRepository
{
    private readonly WalletDbContext _db;

    public ReconciliationRepository(WalletDbContext db) => _db = db;

    public async Task<IReadOnlyList<Guid>> GetAllAccountIdsAsync(CancellationToken cancellationToken) =>
        await _db.Accounts.Select(a => a.Id).ToListAsync(cancellationToken);

    public async Task<AccountLedgerSums> GetAccountLedgerSumsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var linqSum = await _db.LedgerEntries
            .Where(e => e.AccountId == accountId)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

        var entryCount = await _db.LedgerEntries.CountAsync(e => e.AccountId == accountId, cancellationToken);

        // Independent ground truth - hand-written SQL rather than the LINQ provider's
        // translation, so a bug in EF's SQL generation (or a future change to
        // GetBalanceAsync that quietly breaks it) has something to disagree with.
        // SqlQuery<T> requires the single result column named "Value" (case-sensitive) -
        // Postgres folds unquoted identifiers to lowercase, so the alias must be quoted.
        var rawSqlSum = await _db.Database
            .SqlQuery<decimal>(
                $"SELECT COALESCE(SUM(\"Amount\"), 0) AS \"Value\" FROM \"LedgerEntries\" WHERE \"AccountId\" = {accountId}")
            .SingleAsync(cancellationToken);

        return new AccountLedgerSums(linqSum, rawSqlSum, entryCount);
    }
}
