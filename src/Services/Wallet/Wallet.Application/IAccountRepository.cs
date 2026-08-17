using Wallet.Domain;

namespace Wallet.Application;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Sums LedgerEntries for the account - there is no stored balance column.</summary>
    Task<decimal> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken);

    Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);

    void AddLedgerEntry(LedgerEntry entry);

    /// <summary>
    /// Enqueues a domain event (e.g. WalletDebited) for outbox dispatch - written to
    /// the same underlying transaction as the LedgerEntry insert via the next
    /// SaveChangesAsync call, so the ledger write and the event it describes can
    /// never go out of sync with each other (docs/Architecture.md's transactional
    /// outbox pattern). Serialization of <paramref name="payload"/> is an
    /// Infrastructure concern - Application only supplies the event's type name and
    /// the object to serialize.
    /// </summary>
    void EnqueueEvent(string type, object payload);

    /// <summary>
    /// Separate from AddLedgerEntry so the Account row's LastModifiedAtUtc bump and
    /// the new LedgerEntry insert commit atomically in one SaveChangesAsync call -
    /// same reasoning as Identity's IRefreshTokenRepository (rotation needs the
    /// revoke-old and add-new writes to succeed or fail together).
    /// </summary>
    /// <exception cref="ConcurrencyConflictException">
    /// The tracked Account's RowVersion no longer matches the database - another
    /// debit/credit committed first.
    /// </exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Recovery step after a ConcurrencyConflictException: discards whatever
    /// LedgerEntry/OutboxMessage rows the failed attempt had staged (so a retry
    /// doesn't try to insert them a second time and collide with itself on the
    /// idempotency-key unique index), and refreshes <paramref name="account"/>'s
    /// tracked state - including RowVersion - from the database, so the next
    /// attempt's concurrency check is against the current row, not the stale one
    /// that just lost the race.
    /// </summary>
    Task ReloadAsync(Account account, CancellationToken cancellationToken);
}
