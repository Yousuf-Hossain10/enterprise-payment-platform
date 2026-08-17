namespace Wallet.Domain;

/// <summary>
/// Published (via outbox, Wallet.Infrastructure) once a credit's LedgerEntry has
/// been durably written - see WalletDebited for the same reasoning.
/// </summary>
public record WalletCredited(
    Guid AccountId, decimal Amount, string Reference, string IdempotencyKey, DateTime OccurredAtUtc);
