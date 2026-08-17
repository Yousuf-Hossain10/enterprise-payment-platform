namespace Wallet.Domain;

/// <summary>
/// Published (via outbox, Wallet.Infrastructure) once a debit's LedgerEntry has been
/// durably written - past tense, since it describes something that already
/// happened, per docs/Coding-Standards.md's event naming convention. Amount is the
/// debited magnitude (positive), not the signed ledger delta - this is a business
/// event for consumers (Notification, Audit), not a raw ledger row.
/// </summary>
public record WalletDebited(
    Guid AccountId, decimal Amount, string Reference, string IdempotencyKey, DateTime OccurredAtUtc);
