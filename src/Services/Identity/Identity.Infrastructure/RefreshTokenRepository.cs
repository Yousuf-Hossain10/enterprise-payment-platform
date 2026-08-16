using Identity.Application;
using Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IdentityDbContext _db;

    public RefreshTokenRepository(IdentityDbContext db) => _db = db;

    public void Add(RefreshToken token) => _db.RefreshTokens.Add(token);

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        _db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _db.SaveChangesAsync(cancellationToken);
}
