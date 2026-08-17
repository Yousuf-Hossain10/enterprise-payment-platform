using BuildingBlocks.Common;

namespace Payment.Application;

public record GetPaymentByIdQuery(Guid PaymentId);

public class GetPaymentByIdQueryHandler
{
    private readonly IPaymentRepository _payments;

    public GetPaymentByIdQueryHandler(IPaymentRepository payments) => _payments = payments;

    public async Task<Result<Payment.Domain.Payment>> HandleAsync(
        GetPaymentByIdQuery query, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByIdAsync(query.PaymentId, cancellationToken);
        return payment is null
            ? Result<Payment.Domain.Payment>.Failure("Payment not found.")
            : Result<Payment.Domain.Payment>.Success(payment);
    }
}
