using BuildingBlocks.Common;
using Wallet.Domain;

namespace Wallet.Application;

/// <summary>
/// The write step shared by Debit and Credit once each has finished its own checks
/// (idempotency, insufficient-funds where relevant): bump Account.LastModifiedAtUtc
/// so xmin actually changes, add the LedgerEntry, save, and translate a concurrency
/// conflict into a Result failure rather than letting it propagate as an exception.
/// </summary>
internal static class LedgerWriter
{
    public static async Task<Result<decimal>> ApplyAsync(
        IAccountRepository accounts,
        Account account,
        decimal balanceBeforeEntry,
        decimal signedAmount,
        string idempotencyKey,
        string reference,
        CancellationToken cancellationToken)
    {
        account.LastModifiedAtUtc = DateTime.UtcNow;

        accounts.AddLedgerEntry(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            Amount = signedAmount,
            Reference = reference,
            IdempotencyKey = idempotencyKey,
            OccurredAtUtc = DateTime.UtcNow
        });

        try
        {
            await accounts.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<decimal>.Failure("Concurrent modification - retry.");
        }

        return Result<decimal>.Success(balanceBeforeEntry + signedAmount);
    }
}
