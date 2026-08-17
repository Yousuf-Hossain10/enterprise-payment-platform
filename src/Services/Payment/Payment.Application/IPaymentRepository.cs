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

    Task SaveAsync(Payment.Domain.Payment payment, CancellationToken cancellationToken);
}
