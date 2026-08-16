namespace Wallet.Domain;

/// <summary>
/// No mutable Balance column by design (docs/Enterprise_Payment_Platform_Tutorial.md,
/// Phase 6) - balance is always computed by summing LedgerEntries, never stored and
/// risked going out of sync with the ledger that is the actual source of truth.
/// RowVersion is the optimistic concurrency token EF Core uses to detect (and reject)
/// concurrent writes racing on the same account - backed by Postgres' native `xmin`
/// system column (Wallet.Infrastructure/Configurations/AccountConfiguration.cs)
/// rather than an app-maintained counter, since Postgres has no SQL Server-style
/// auto-incrementing rowversion type.
/// </summary>
public class Account
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Currency { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Bumped on every debit/credit (Day 24) alongside the LedgerEntry insert - not
    /// used for anything on its own, but without it the Account row is never actually
    /// written when a ledger entry is added, so xmin would never change and the
    /// RowVersion concurrency check would never engage. This is what turns
    /// SaveChangesAsync into the serialization point two racing debits collide on.
    /// </summary>
    public DateTime LastModifiedAtUtc { get; set; }

    public uint RowVersion { get; set; }
}
