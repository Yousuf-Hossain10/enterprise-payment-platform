using BuildingBlocks.Common;
using FluentValidation;

namespace Payment.Application;

public record CreatePaymentCommand(Guid AccountId, decimal Amount, string Reference, string IdempotencyKey);

public class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Reference).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty();
    }
}

public class CreatePaymentCommandHandler
{
    private readonly IPaymentRepository _payments;
    private readonly IValidator<CreatePaymentCommand> _validator;

    public CreatePaymentCommandHandler(IPaymentRepository payments, IValidator<CreatePaymentCommand> validator)
    {
        _payments = payments;
        _validator = validator;
    }

    public async Task<Result<Payment.Domain.Payment>> HandleAsync(
        CreatePaymentCommand command, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<Payment.Domain.Payment>.Failure(
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        // Idempotency-Key replay: the same key returns the original payment rather
        // than creating a second one - docs/API-Guidelines.md's "same status, same
        // body" guarantee for a retried financial write, enforced here by the real
        // unique index on IdempotencyKey (Day 33's PaymentConfiguration), not just
        // this check - this check just avoids racing toward that constraint
        // unnecessarily.
        var existing = await _payments.GetByIdempotencyKeyAsync(command.IdempotencyKey, cancellationToken);
        if (existing is not null)
            return Result<Payment.Domain.Payment>.Success(existing);

        var payment = new Payment.Domain.Payment
        {
            Id = Guid.NewGuid(),
            AccountId = command.AccountId,
            Amount = command.Amount,
            Reference = command.Reference,
            IdempotencyKey = command.IdempotencyKey,
            CreatedAtUtc = DateTime.UtcNow
        };

        _payments.Add(payment);
        await _payments.SaveAsync(payment, cancellationToken);

        return Result<Payment.Domain.Payment>.Success(payment);
    }
}
