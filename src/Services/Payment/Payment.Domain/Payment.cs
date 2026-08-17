namespace Payment.Domain;

/// <summary>
/// The state machine itself: which transitions are legal from which status, enforced
/// by every Mark* method rather than left to caller discipline. AccountId/Amount
/// identify the Wallet account and amount this payment will debit once the saga
/// (Day 33) authorizes and captures it.
/// </summary>
public class Payment
{
    private static readonly Dictionary<PaymentStatus, PaymentStatus[]> LegalTransitions = new()
    {
        [PaymentStatus.Created] = [PaymentStatus.Authorized, PaymentStatus.Failed],
        [PaymentStatus.Authorized] = [PaymentStatus.Captured, PaymentStatus.Failed],
        [PaymentStatus.Captured] = [PaymentStatus.Settled, PaymentStatus.Refunded],
        [PaymentStatus.Settled] = [PaymentStatus.Refunded],
        [PaymentStatus.Failed] = [],
        [PaymentStatus.Refunded] = []
    };

    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }
    public string Reference { get; set; } = default!;
    public string IdempotencyKey { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastModifiedAtUtc { get; set; }

    public PaymentStatus Status { get; private set; } = PaymentStatus.Created;
    public string? FailureReason { get; private set; }

    /// <exception cref="InvalidPaymentStateTransitionException">
    /// Authorization is only legal from Created.
    /// </exception>
    public void MarkAuthorized() => TransitionTo(PaymentStatus.Authorized);

    /// <exception cref="InvalidPaymentStateTransitionException">
    /// Capture is only legal from Authorized.
    /// </exception>
    public void MarkCaptured() => TransitionTo(PaymentStatus.Captured);

    /// <exception cref="InvalidPaymentStateTransitionException">
    /// Settlement is only legal from Captured.
    /// </exception>
    public void MarkSettled() => TransitionTo(PaymentStatus.Settled);

    /// <exception cref="InvalidPaymentStateTransitionException">
    /// Refund is only legal from Captured or Settled - a payment that never
    /// captured has nothing to refund.
    /// </exception>
    public void MarkRefunded() => TransitionTo(PaymentStatus.Refunded);

    /// <exception cref="InvalidPaymentStateTransitionException">
    /// Failure is only legal from Created or Authorized - once a payment has
    /// captured, it can no longer simply "fail"; a captured payment that needs
    /// reversing is a refund, not a failure.
    /// </exception>
    public void MarkFailed(string reason)
    {
        TransitionTo(PaymentStatus.Failed);
        FailureReason = reason;
    }

    private void TransitionTo(PaymentStatus newStatus)
    {
        if (!LegalTransitions[Status].Contains(newStatus))
            throw new InvalidPaymentStateTransitionException(Status, newStatus);

        Status = newStatus;
        LastModifiedAtUtc = DateTime.UtcNow;
    }
}
