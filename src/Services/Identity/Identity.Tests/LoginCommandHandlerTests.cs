using Identity.Application;
using Identity.Domain;
using NSubstitute;

namespace Identity.Tests;

public class LoginCommandHandlerTests
{
    private static LoginCommandHandler CreateSut(
        out IUserRepository users,
        out IPasswordHasher hasher,
        out ITokenService tokenService,
        out IRefreshTokenRepository refreshTokens,
        out IRefreshTokenHasher refreshTokenHasher)
    {
        users = Substitute.For<IUserRepository>();
        hasher = Substitute.For<IPasswordHasher>();
        tokenService = Substitute.For<ITokenService>();
        refreshTokens = Substitute.For<IRefreshTokenRepository>();
        refreshTokenHasher = Substitute.For<IRefreshTokenHasher>();
        return new LoginCommandHandler(
            users, hasher, new LoginCommandValidator(), tokenService, refreshTokens, refreshTokenHasher);
    }

    private static TokenPair SampleTokens() => new(
        "access-token", DateTime.UtcNow.AddMinutes(15), "refresh-token", DateTime.UtcNow.AddDays(14));

    [Fact]
    public async Task Succeeds_AndIssuesAndStoresTokens_ForCorrectCredentials()
    {
        var sut = CreateSut(out var users, out var hasher, out var tokenService, out var refreshTokens, out var refreshTokenHasher);
        var user = new User { Id = Guid.NewGuid(), Email = "user@example.com", PasswordHash = "stored-hash" };
        users.GetByEmailAsync("user@example.com", Arg.Any<CancellationToken>()).Returns(user);
        hasher.Verify("correct-password", "stored-hash").Returns(true);
        var tokens = SampleTokens();
        tokenService.IssueAsync(user, Arg.Any<CancellationToken>()).Returns(tokens);
        refreshTokenHasher.Hash("refresh-token").Returns("hashed-refresh-token");

        var result = await sut.HandleAsync(
            new LoginCommand("user@example.com", "correct-password"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(tokens, result.Value);
        refreshTokens.Received(1).Add(Arg.Is<RefreshToken>(t =>
            t.UserId == user.Id &&
            t.TokenHash == "hashed-refresh-token" &&
            t.ExpiresAtUtc == tokens.RefreshTokenExpiresAtUtc &&
            !t.Revoked));
        await refreshTokens.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_ForWrongPassword_AndDoesNotIssueTokens()
    {
        var sut = CreateSut(out var users, out var hasher, out var tokenService, out _, out _);
        var user = new User { Id = Guid.NewGuid(), Email = "user@example.com", PasswordHash = "stored-hash" };
        users.GetByEmailAsync("user@example.com", Arg.Any<CancellationToken>()).Returns(user);
        hasher.Verify("wrong-password", "stored-hash").Returns(false);

        var result = await sut.HandleAsync(
            new LoginCommand("user@example.com", "wrong-password"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        await tokenService.DidNotReceiveWithAnyArgs().IssueAsync(default!, default);
    }

    [Fact]
    public async Task Fails_ForUnknownEmail_WithSameErrorAsWrongPassword()
    {
        var sut = CreateSut(out var users, out _, out _, out _, out _);
        users.GetByEmailAsync("nobody@example.com", Arg.Any<CancellationToken>()).Returns((User?)null);

        var unknownEmailResult = await sut.HandleAsync(
            new LoginCommand("nobody@example.com", "whatever"), CancellationToken.None);

        var sut2 = CreateSut(out var users2, out var hasher2, out _, out _, out _);
        var user = new User { Id = Guid.NewGuid(), Email = "user@example.com", PasswordHash = "stored-hash" };
        users2.GetByEmailAsync("user@example.com", Arg.Any<CancellationToken>()).Returns(user);
        hasher2.Verify("wrong-password", "stored-hash").Returns(false);
        var wrongPasswordResult = await sut2.HandleAsync(
            new LoginCommand("user@example.com", "wrong-password"), CancellationToken.None);

        // Deliberately identical error message - a distinct one would let a caller
        // enumerate which emails are registered.
        Assert.Equal(wrongPasswordResult.Error, unknownEmailResult.Error);
    }
}
