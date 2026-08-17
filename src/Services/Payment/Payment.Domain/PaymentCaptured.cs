namespace Payment.Domain;

/// <summary>
/// Published (via outbox, Payment.Infrastructure) once a payment reaches the
/// terminal Captured status - past tense, since it describes something that
/// already happened, per docs/Coding-Standards.md's event naming convention.
/// </summary>
public record PaymentCaptured(Guid PaymentId, Guid AccountId, decimal Amount, string Reference, DateTime OccurredAtUtc);
