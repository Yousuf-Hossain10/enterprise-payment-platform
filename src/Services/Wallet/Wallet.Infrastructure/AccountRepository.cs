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
}
