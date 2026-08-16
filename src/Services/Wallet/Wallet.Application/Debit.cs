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

        // Bump LastModifiedAtUtc so the Account row is actually written alongside
        // the LedgerEntry insert - otherwise xmin never changes and the RowVersion
        // concurrency check below never engages (see Wallet.Domain/Account.cs).
        account.LastModifiedAtUtc = DateTime.UtcNow;

        _accounts.AddLedgerEntry(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            AccountId = command.AccountId,
            Amount = -command.Amount,
            Reference = command.Reference,
            IdempotencyKey = command.IdempotencyKey,
            OccurredAtUtc = DateTime.UtcNow
        });

        try
        {
            await _accounts.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<decimal>.Failure("Concurrent modification - retry.");
        }

        return Result<decimal>.Success(balance - command.Amount);
    }
}
