using Identity.Domain;

namespace Identity.Application;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);

    // Lookup/rotation methods land Day 20 alongside refresh-token rotation.
}
