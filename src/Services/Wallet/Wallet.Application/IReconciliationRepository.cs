namespace Wallet.Application;

public interface IReconciliationRepository
{
    Task<IReadOnlyList<Guid>> GetAllAccountIdsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Computes an account's ledger sum two independent ways: the same LINQ-translated
    /// SQL every other balance query in this service uses, and a hand-written raw SQL
    /// query as an independent ground truth. If they ever diverge, that's a bug in the
    /// ORM translation or the schema, not a data-drift warning - there is no separately
    /// maintained cached balance in this system to drift from (Wallet.Domain/Account.cs -
    /// no Balance column by design), so this cross-check is what "reconciliation" means
    /// here.
    /// </summary>
    Task<AccountLedgerSums> GetAccountLedgerSumsAsync(Guid accountId, CancellationToken cancellationToken);
}

public record AccountLedgerSums(decimal LinqSum, decimal RawSqlSum, int LedgerEntryCount);
