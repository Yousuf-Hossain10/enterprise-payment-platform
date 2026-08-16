using Identity.Domain;

namespace Identity.Application;

/// <summary>
/// Add/SaveChangesAsync are split (rather than one auto-saving AddAsync) so
/// rotation can revoke the old token and add its replacement in a single
/// atomic SaveChangesAsync - if those happened as two separate saves, a
/// failure between them could revoke a token without ever persisting its
/// replacement, locking the caller out.
/// </summary>
public interface IRefreshTokenRepository
{
    void Add(RefreshToken token);

    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
