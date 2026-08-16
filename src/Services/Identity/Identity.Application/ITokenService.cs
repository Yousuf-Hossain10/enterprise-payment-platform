using Identity.Domain;

namespace Identity.Application;

public interface ITokenService
{
    Task<TokenPair> IssueAsync(User user, CancellationToken cancellationToken);
}
