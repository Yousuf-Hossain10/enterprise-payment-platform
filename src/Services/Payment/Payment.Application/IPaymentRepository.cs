namespace Payment.Application;

// Fully-qualified Payment.Domain.Payment throughout this project's Application/
// Infrastructure layers - "Payment" is both this solution's root namespace prefix
// and the entity's own class name, so a bare `using Payment.Domain;` leaves the
// identifier ambiguous.

public interface IPaymentRepository
{
    Task<Payment.Domain.Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken);

    Task<Payment.Domain.Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);

    void Add(Payment.Domain.Payment payment);

    /// <summary>
    /// Enqueues a domain event (e.g. PaymentCaptured) for outbox dispatch - written
    /// to the same underlying transaction as the payment's SaveAsync call, so the
    /// write and the event describing it can never go out of sync
    /// (docs/Architecture.md's transactional outbox pattern). Serialization is an
    /// Infrastructure concern - Application only supplies the event's type name and
    /// the object to serialize.
    /// </summary>
    void EnqueueEvent(string type, object payload);

    Task SaveAsync(Payment.Domain.Payment payment, CancellationToken cancellationToken);
}
