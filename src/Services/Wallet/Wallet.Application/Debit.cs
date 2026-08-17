using BuildingBlocks.Common;
using FluentValidation;
using Wallet.Domain;

namespace Wallet.Application;

public record DebitCommand(Guid AccountId, decimal Amount, string IdempotencyKey, string Reference);

public class DebitCommandValidator : AbstractValidator<DebitCommand>
{
    public DebitCommandValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.IdempotencyKey).NotEmpty();
        RuleFor(x => x.Reference).NotEmpty();
    }
}

public class DebitCommandHandler
{
    private readonly IAccountRepository _accounts;
    private readonly IValidator<DebitCommand> _validator;

    public DebitCommandHandler(IAccountRepository accounts, IValidator<DebitCommand> validator)
    {
        _accounts = accounts;
        _validator = validator;
    }

    public async Task<Result<decimal>> HandleAsync(DebitCommand command, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<decimal>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        // Idempotency check first - a retried request must be a no-op, not a double
        // debit. The unique index on LedgerEntry.IdempotencyKey is the real
        // enforcement; this check just avoids racing toward it unnecessarily.
        if (await _accounts.ExistsByIdempotencyKeyAsync(command.IdempotencyKey, cancellationToken))
            return Result<decimal>.Success(await _accounts.GetBalanceAsync(command.AccountId, cancellationToken));

        var account = await _accounts.GetByIdAsync(command.AccountId, cancellationToken);
        if (account is null)
            return Result<decimal>.Failure("Account not found.");

        var balance = await _accounts.GetBalanceAsync(command.AccountId, cancellationToken);
        if (balance < command.Amount)
            return Result<decimal>.Failure("Insufficient funds.");

        var occurredAtUtc = DateTime.UtcNow;
        return await LedgerWriter.ApplyAsync(
            _accounts, account, balance, -command.Amount, command.IdempotencyKey, command.Reference,
            nameof(WalletDebited),
            new WalletDebited(command.AccountId, command.Amount, command.Reference, command.IdempotencyKey, occurredAtUtc),
            cancellationToken);
    }
}
