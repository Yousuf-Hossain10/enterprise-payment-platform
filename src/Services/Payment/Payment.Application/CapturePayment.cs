using BuildingBlocks.Common;
using FluentValidation;
using Payment.Domain;

namespace Payment.Application;

public record CapturePaymentCommand(Guid PaymentId, string IdempotencyKey, string? BearerToken);

public class CapturePaymentCommandValidator : AbstractValidator<CapturePaymentCommand>
{
    public CapturePaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty();
    }
}

/// <summary>
/// The saga (docs/Enterprise_Payment_Platform_Tutorial.md Phase 7): orchestrate the
/// synchronous call to Wallet without ever leaving a payment stuck. Deliberately
/// matches the tutorial's own CapturePaymentAsync snippet, including *not* saving
/// the in-memory MarkAuthorized() transition on its own - only the terminal outcome
/// (Failed or Captured) is ever persisted. If the process crashes between
/// MarkAuthorized() and the Wallet call returning, the database still shows
/// Created, so a retried capture starts clean instead of getting stuck in a
/// half-persisted Authorized limbo that would need special recovery logic.
/// </summary>
public class CapturePaymentCommandHandler
{
    private readonly IPaymentRepository _payments;
    private readonly IWalletClient _walletClient;
    private readonly IValidator<CapturePaymentCommand> _validator;

    public CapturePaymentCommandHandler(
        IPaymentRepository payments, IWalletClient walletClient, IValidator<CapturePaymentCommand> validator)
    {
        _payments = payments;
        _walletClient = walletClient;
        _validator = validator;
    }

    public async Task<Result<Payment.Domain.Payment>> HandleAsync(
        CapturePaymentCommand command, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<Payment.Domain.Payment>.Failure(
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var payment = await _payments.GetByIdAsync(command.PaymentId, cancellationToken);
        if (payment is null)
            return Result<Payment.Domain.Payment>.Failure("Payment not found.");

        try
        {
            payment.MarkAuthorized();
        }
        catch (InvalidPaymentStateTransitionException ex)
        {
            // Not persisted (see class remarks) - this payment's stored state is
            // untouched, so nothing here needs cleaning up.
            return Result<Payment.Domain.Payment>.Failure(ex.Message);
        }

        var debitResult = await _walletClient.DebitAsync(
            payment.AccountId, payment.Amount, command.IdempotencyKey, payment.Id.ToString(),
            command.BearerToken, cancellationToken);

        if (!debitResult.IsSuccess)
        {
            payment.MarkFailed(debitResult.Error!);
            _payments.EnqueueEvent(nameof(PaymentFailed), new PaymentFailed(
                payment.Id, payment.AccountId, payment.Amount, payment.Reference,
                debitResult.Error!, DateTime.UtcNow));
            await _payments.SaveAsync(payment, cancellationToken);
            return Result<Payment.Domain.Payment>.Failure(debitResult.Error!);
        }

        payment.MarkCaptured();
        _payments.EnqueueEvent(nameof(PaymentCaptured), new PaymentCaptured(
            payment.Id, payment.AccountId, payment.Amount, payment.Reference, DateTime.UtcNow));
        await _payments.SaveAsync(payment, cancellationToken);
        return Result<Payment.Domain.Payment>.Success(payment);
    }
}
