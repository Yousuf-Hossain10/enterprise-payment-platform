namespace Payment.Domain;

/// <summary>
/// Published (via outbox, Payment.Infrastructure) once a payment reaches the
/// terminal Failed status - see PaymentCaptured for the same reasoning.
/// </summary>
public record PaymentFailed(
    Guid PaymentId, Guid AccountId, decimal Amount, string Reference, string FailureReason, DateTime OccurredAtUtc);
