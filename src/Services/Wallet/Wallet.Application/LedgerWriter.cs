using BuildingBlocks.Common;
using Wallet.Domain;

namespace Wallet.Application;

/// <summary>
/// The write step shared by Debit and Credit once each has finished its initial
/// idempotency/account-lookup checks: bump Account.LastModifiedAtUtc so xmin
/// actually changes, add the LedgerEntry and outbox event, save, and retry on a
/// concurrency conflict rather than failing on the first race lost - see
/// docs/Enterprise_Payment_Platform_Tutorial.md Phase 6's concurrency test and
/// CLAUDE.md's Day 27/28 notes for why this exists.
///
/// A fixed small MaxAttempts with immediate (non-jittered) retry was tried first
/// and measured empirically against the Day 27 test (20 concurrent debits, 10
/// theoretically satisfiable): it converged on exactly 6 successes, deterministically,
/// every run - not "occasionally short," but structurally short. The reason: every
/// losing attempt reloads and retries at essentially the same instant as every other
/// loser, so they collide again in lockstep each round - each retry "wave" produces
/// almost exactly one winner regardless of how many contenders remain, capping total
/// successes at roughly MaxAttempts, not at the account's real capacity. Small
/// randomized backoff before each retry de-synchronizes the herd so losers spread
/// across time instead of re-colliding as one block, and a higher attempt ceiling
/// gives that spreading room to actually pay off.
/// </summary>
internal static class LedgerWriter
{
    private const int MaxAttempts = 25;
    private static readonly Random JitterRandom = new();

    public static async Task<Result<decimal>> ApplyAsync(
        IAccountRepository accounts,
        Account account,
        decimal balanceBeforeEntry,
        decimal signedAmount,
        string idempotencyKey,
        string reference,
        string eventType,
        object eventPayload,
        Func<decimal, Result<decimal>?>? validateBalance,
        CancellationToken cancellationToken)
    {
        var balance = balanceBeforeEntry;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var validationFailure = validateBalance?.Invoke(balance);
            if (validationFailure is not null)
                return validationFailure;

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

            // Same SaveChangesAsync as the LedgerEntry insert - the event can never
            // be published for a write that didn't durably commit, or vice versa.
            accounts.EnqueueEvent(eventType, eventPayload);

            try
            {
                await accounts.SaveChangesAsync(cancellationToken);
                return Result<decimal>.Success(balance + signedAmount);
            }
            catch (ConcurrencyConflictException)
            {
                if (attempt == MaxAttempts)
                    return Result<decimal>.Failure("Concurrent modification - retry.");

                // Jittered backoff before retrying - de-synchronizes a herd of
                // simultaneous losers so they don't all collide again in lockstep
                // on the very next attempt (see class remarks: without this, retry
                // count barely moves the needle on total successes).
                await Task.Delay(JitterRandom.Next(1, 20), cancellationToken);

                // Someone else's debit/credit committed first - re-read the account
                // (fresh RowVersion) and balance, then loop to re-validate and retry
                // against current state rather than the stale snapshot that lost.
                await accounts.ReloadAsync(account, cancellationToken);
                balance = await accounts.GetBalanceAsync(account.Id, cancellationToken);
            }
        }

        return Result<decimal>.Failure("Concurrent modification - retry.");
    }
}
