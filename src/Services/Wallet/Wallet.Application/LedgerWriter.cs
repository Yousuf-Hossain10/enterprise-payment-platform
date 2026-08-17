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
        string eventType,
        object eventPayload,
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

        // Same SaveChangesAsync as the LedgerEntry insert - the event can never be
        // published for a write that didn't durably commit, or vice versa.
        accounts.EnqueueEvent(eventType, eventPayload);

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
