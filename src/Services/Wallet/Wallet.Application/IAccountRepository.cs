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
}
