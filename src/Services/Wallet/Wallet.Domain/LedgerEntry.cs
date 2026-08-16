namespace Wallet.Domain;

/// <summary>
/// Immutable, append-only - the source of truth every Account balance is computed
/// from (docs/Enterprise_Payment_Platform_Tutorial.md, Phase 6). Never updated or
/// deleted once written. IdempotencyKey is unique per entry so a retried debit/credit
/// request is a no-op rather than a double-write.
/// </summary>
public class LedgerEntry
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }

    /// <summary>Positive = credit, negative = debit.</summary>
    public decimal Amount { get; set; }

    /// <summary>What this entry is for, e.g. a payment id.</summary>
    public string Reference { get; set; } = default!;
    public string IdempotencyKey { get; set; } = default!;
    public DateTime OccurredAtUtc { get; set; }
}
