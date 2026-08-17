namespace Payment.Domain;

/// <summary>
/// Created -&gt; Authorized -&gt; Captured -&gt; Settled is the happy path; Failed and
/// Refunded are terminal/branch states (docs/Enterprise_Payment_Platform_Tutorial.md,
/// Phase 7). Modeled as an enum rather than a free-text status string specifically so
/// illegal transitions are a compile-time-checkable, exhaustively-testable set
/// (see Payment.LegalTransitions) instead of something every caller has to get right
/// by convention.
/// </summary>
public enum PaymentStatus
{
    Created,
    Authorized,
    Captured,
    Settled,
    Failed,
    Refunded
}
