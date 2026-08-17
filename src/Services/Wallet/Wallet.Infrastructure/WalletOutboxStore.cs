using BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Wallet.Infrastructure;

/// <summary>
/// IOutboxStore implementation OutboxDispatcherBackgroundService polls against -
/// a separate concern from AccountRepository.EnqueueEvent, which only ever writes
/// new rows as part of a debit/credit's own transaction. This is read/mark-processed
/// only, on its own scoped WalletDbContext per poll cycle.
/// </summary>
public class WalletOutboxStore : IOutboxStore
{
    private readonly WalletDbContext _db;

    public WalletOutboxStore(WalletDbContext db) => _db = db;

    public async Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken) =>
        await _db.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null)
            .OrderBy(m => m.OccurredAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public async Task MarkProcessedAsync(Guid id, DateTime processedAtUtc, CancellationToken cancellationToken)
    {
        var message = await _db.OutboxMessages.SingleAsync(m => m.Id == id, cancellationToken);
        message.ProcessedAtUtc = processedAtUtc;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
