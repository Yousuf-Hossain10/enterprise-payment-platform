using BuildingBlocks.Common;
using FluentValidation;
using Wallet.Domain;

namespace Wallet.Application;

public record CreditCommand(Guid AccountId, decimal Amount, string IdempotencyKey, string Reference);

public class CreditCommandValidator : AbstractValidator<CreditCommand>
{
    public CreditCommandValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.IdempotencyKey).NotEmpty();
        RuleFor(x => x.Reference).NotEmpty();
    }
}

public class CreditCommandHandler
{
    private readonly IAccountRepository _accounts;
    private readonly IValidator<CreditCommand> _validator;

    public CreditCommandHandler(IAccountRepository accounts, IValidator<CreditCommand> validator)
    {
        _accounts = accounts;
        _validator = validator;
    }

    public async Task<Result<decimal>> HandleAsync(CreditCommand command, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<decimal>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        // Idempotency check first - a retried request must be a no-op, not a double
        // credit. The unique index on LedgerEntry.IdempotencyKey is the real
        // enforcement; this check just avoids racing toward it unnecessarily.
        if (await _accounts.ExistsByIdempotencyKeyAsync(command.IdempotencyKey, cancellationToken))
            return Result<decimal>.Success(await _accounts.GetBalanceAsync(command.AccountId, cancellationToken));

        var account = await _accounts.GetByIdAsync(command.AccountId, cancellationToken);
        if (account is null)
            return Result<decimal>.Failure("Account not found.");

        // No upper bound to check - unlike Debit, a credit can never overdraw an
        // account, so there's nothing here analogous to the insufficient-funds guard.
        var balance = await _accounts.GetBalanceAsync(command.AccountId, cancellationToken);

        var occurredAtUtc = DateTime.UtcNow;
        return await LedgerWriter.ApplyAsync(
            _accounts, account, balance, command.Amount, command.IdempotencyKey, command.Reference,
            nameof(WalletCredited),
            new WalletCredited(command.AccountId, command.Amount, command.Reference, command.IdempotencyKey, occurredAtUtc),
            validateBalance: null,
            cancellationToken);
    }
}
