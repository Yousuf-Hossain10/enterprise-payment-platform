using Identity.Application;
using Identity.Domain;
using NSubstitute;
using DomainRefreshToken = Identity.Domain.RefreshToken;

namespace Identity.Tests;

public class RefreshCommandHandlerTests
{
    private static RefreshCommandHandler CreateSut(
        out IRefreshTokenRepository refreshTokens,
        out IRefreshTokenHasher refreshTokenHasher,
        out IUserRepository users,
        out ITokenService tokenService)
    {
        refreshTokens = Substitute.For<IRefreshTokenRepository>();
        refreshTokenHasher = Substitute.For<IRefreshTokenHasher>();
        users = Substitute.For<IUserRepository>();
        tokenService = Substitute.For<ITokenService>();
        return new RefreshCommandHandler(refreshTokens, refreshTokenHasher, users, tokenService, new RefreshCommandValidator());
    }

    private static DomainRefreshToken ValidStoredToken(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TokenHash = "hashed-old-token",
        ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
        Revoked = false,
        CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
    };

    [Fact]
    public async Task Succeeds_RevokesOldToken_AndStoresNewOne_ForValidRefreshToken()
    {
        var sut = CreateSut(out var refreshTokens, out var hasher, out var users, out var tokenService);
        var user = new User { Id = Guid.NewGuid(), Email = "user@example.com", PasswordHash = "x" };
        var stored = ValidStoredToken(user.Id);
        hasher.Hash("old-token").Returns("hashed-old-token");
        refreshTokens.GetByTokenHashAsync("hashed-old-token", Arg.Any<CancellationToken>()).Returns(stored);
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var newTokens = new TokenPair("new-access", DateTime.UtcNow.AddMinutes(15), "new-refresh", DateTime.UtcNow.AddDays(14));
        tokenService.IssueAsync(user, Arg.Any<CancellationToken>()).Returns(newTokens);
        hasher.Hash("new-refresh").Returns("hashed-new-token");

        var result = await sut.HandleAsync(new RefreshCommand("old-token"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(newTokens, result.Value);
        Assert.True(stored.Revoked);
        Assert.Equal("hashed-new-token", stored.ReplacedByTokenHash);
        refreshTokens.Received(1).Add(Arg.Is<DomainRefreshToken>(t =>
            t.UserId == user.Id && t.TokenHash == "hashed-new-token" && !t.Revoked));
        await refreshTokens.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_WhenTokenNotFound()
    {
        var sut = CreateSut(out var refreshTokens, out var hasher, out _, out _);
        hasher.Hash("unknown-token").Returns("hashed-unknown");
        refreshTokens.GetByTokenHashAsync("hashed-unknown", Arg.Any<CancellationToken>()).Returns((DomainRefreshToken?)null);

        var result = await sut.HandleAsync(new RefreshCommand("unknown-token"), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Fails_WhenTokenAlreadyRevoked_RejectingReuse()
    {
        var sut = CreateSut(out var refreshTokens, out var hasher, out _, out var tokenService);
        var stored = ValidStoredToken(Guid.NewGuid());
        stored.Revoked = true;
        hasher.Hash("reused-token").Returns("hashed-old-token");
        refreshTokens.GetByTokenHashAsync("hashed-old-token", Arg.Any<CancellationToken>()).Returns(stored);

        var result = await sut.HandleAsync(new RefreshCommand("reused-token"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        await tokenService.DidNotReceiveWithAnyArgs().IssueAsync(default!, default);
    }

    [Fact]
    public async Task Fails_WhenTokenExpired()
    {
        var sut = CreateSut(out var refreshTokens, out var hasher, out _, out _);
        var stored = ValidStoredToken(Guid.NewGuid());
        stored.ExpiresAtUtc = DateTime.UtcNow.AddDays(-1);
        hasher.Hash("expired-token").Returns("hashed-old-token");
        refreshTokens.GetByTokenHashAsync("hashed-old-token", Arg.Any<CancellationToken>()).Returns(stored);

        var result = await sut.HandleAsync(new RefreshCommand("expired-token"), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
