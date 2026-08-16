using Identity.Application;
using NSubstitute;
using DomainRefreshToken = Identity.Domain.RefreshToken;

namespace Identity.Tests;

public class RevokeCommandHandlerTests
{
    private static RevokeCommandHandler CreateSut(
        out IRefreshTokenRepository refreshTokens, out IRefreshTokenHasher refreshTokenHasher)
    {
        refreshTokens = Substitute.For<IRefreshTokenRepository>();
        refreshTokenHasher = Substitute.For<IRefreshTokenHasher>();
        return new RevokeCommandHandler(refreshTokens, refreshTokenHasher, new RevokeCommandValidator());
    }

    [Fact]
    public async Task Succeeds_AndMarksTokenRevoked_ForExistingToken()
    {
        var sut = CreateSut(out var refreshTokens, out var hasher);
        var stored = new DomainRefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = "hashed-token",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
            Revoked = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        hasher.Hash("plain-token").Returns("hashed-token");
        refreshTokens.GetByTokenHashAsync("hashed-token", Arg.Any<CancellationToken>()).Returns(stored);

        var result = await sut.HandleAsync(new RevokeCommand("plain-token"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(stored.Revoked);
        await refreshTokens.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_WhenTokenNotFound()
    {
        var sut = CreateSut(out var refreshTokens, out var hasher);
        hasher.Hash("unknown-token").Returns("hashed-unknown");
        refreshTokens.GetByTokenHashAsync("hashed-unknown", Arg.Any<CancellationToken>()).Returns((DomainRefreshToken?)null);

        var result = await sut.HandleAsync(new RevokeCommand("unknown-token"), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
