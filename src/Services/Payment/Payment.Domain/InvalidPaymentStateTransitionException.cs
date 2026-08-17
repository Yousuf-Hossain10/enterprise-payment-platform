namespace Payment.Domain;

/// <summary>
/// Thrown by Payment's Mark* methods when the requested transition isn't legal from
/// the entity's current status - a business rule violation intrinsic to the entity
/// itself, so it lives in Domain rather than as an Application-layer Result failure
/// (docs/Coding-Standards.md - Domain takes no dependency on BuildingBlocks.Common,
/// which is where Result&lt;T&gt; is defined). The saga orchestrator (Day 33) is expected
/// to catch this and translate it into a Result failure at the Application boundary.
/// </summary>
public class InvalidPaymentStateTransitionException : Exception
{
    public PaymentStatus CurrentStatus { get; }
    public PaymentStatus AttemptedStatus { get; }

    public InvalidPaymentStateTransitionException(PaymentStatus currentStatus, PaymentStatus attemptedStatus)
        : base($"Cannot transition payment from {currentStatus} to {attemptedStatus}.")
    {
        CurrentStatus = currentStatus;
        AttemptedStatus = attemptedStatus;
    }
}
