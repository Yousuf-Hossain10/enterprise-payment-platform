using Identity.Application;
using Identity.Domain;

namespace Identity.Infrastructure;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IdentityDbContext _db;

    public RefreshTokenRepository(IdentityDbContext db) => _db = db;

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
