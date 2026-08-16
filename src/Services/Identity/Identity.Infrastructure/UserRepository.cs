using Identity.Application;
using Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure;

public class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _db;

    public UserRepository(IdentityDbContext db) => _db = db;

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        _db.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
