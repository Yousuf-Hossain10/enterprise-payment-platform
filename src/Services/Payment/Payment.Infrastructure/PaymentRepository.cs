using System.Text.Json;
using BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Payment.Application;

namespace Payment.Infrastructure;

public class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _db;

    public PaymentRepository(PaymentDbContext db) => _db = db;

    public Task<Payment.Domain.Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken) =>
        _db.Payments.SingleOrDefaultAsync(p => p.Id == paymentId, cancellationToken);

    public Task<Payment.Domain.Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        _db.Payments.SingleOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, cancellationToken);

    public void Add(Payment.Domain.Payment payment) => _db.Payments.Add(payment);

    public void EnqueueEvent(string type, object payload) => _db.OutboxMessages.Add(new OutboxMessage
    {
        Id = Guid.NewGuid(),
        Type = type,
        Payload = JsonSerializer.Serialize(payload),
        OccurredAtUtc = DateTime.UtcNow
    });

    public Task SaveAsync(Payment.Domain.Payment payment, CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);
}
