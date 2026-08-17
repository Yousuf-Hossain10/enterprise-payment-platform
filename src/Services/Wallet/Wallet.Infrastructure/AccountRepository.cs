using System.Text.Json;
using BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Wallet.Application;
using Wallet.Domain;

namespace Wallet.Infrastructure;

public class AccountRepository : IAccountRepository
{
    private readonly WalletDbContext _db;

    public AccountRepository(WalletDbContext db) => _db = db;

    public Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken) =>
        _db.Accounts.SingleOrDefaultAsync(a => a.Id == accountId, cancellationToken);

    public async Task<decimal> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var sum = await _db.LedgerEntries
            .Where(e => e.AccountId == accountId)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken);
        return sum ?? 0m;
    }

    public Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        _db.LedgerEntries.AnyAsync(e => e.IdempotencyKey == idempotencyKey, cancellationToken);

    public void AddLedgerEntry(LedgerEntry entry) => _db.LedgerEntries.Add(entry);

    public void EnqueueEvent(string type, object payload) => _db.OutboxMessages.Add(new OutboxMessage
    {
        Id = Guid.NewGuid(),
        Type = type,
        Payload = JsonSerializer.Serialize(payload),
        OccurredAtUtc = DateTime.UtcNow
    });

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("The account was modified concurrently.", ex);
        }
    }

    public async Task ReloadAsync(Account account, CancellationToken cancellationToken)
    {
        // The failed attempt's LedgerEntry/OutboxMessage never committed (the whole
        // SaveChangesAsync call rolled back), but EF's change tracker still has them
        // staged as Added - detach both so the next attempt doesn't try to insert
        // the same idempotency key twice.
        foreach (var entry in _db.ChangeTracker.Entries<LedgerEntry>().Where(e => e.State == EntityState.Added).ToList())
            entry.State = EntityState.Detached;
        foreach (var entry in _db.ChangeTracker.Entries<OutboxMessage>().Where(e => e.State == EntityState.Added).ToList())
            entry.State = EntityState.Detached;

        // Refreshes account's current values - including RowVersion/xmin - from the
        // database. Without this, re-querying GetByIdAsync on the same DbContext
        // would return the same already-tracked (stale) instance rather than a
        // fresh one, since EF's default tracking behavior doesn't overwrite an
        // already-tracked entity's values from a query result.
        await _db.Entry(account).ReloadAsync(cancellationToken);
    }
}
