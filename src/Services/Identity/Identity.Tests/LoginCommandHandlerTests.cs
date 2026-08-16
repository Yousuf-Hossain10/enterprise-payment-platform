using Identity.Application;
using Identity.Domain;
using NSubstitute;

namespace Identity.Tests;

public class LoginCommandHandlerTests
{
    private static LoginCommandHandler CreateSut(out IUserRepository users, out IPasswordHasher hasher)
    {
        users = Substitute.For<IUserRepository>();
        hasher = Substitute.For<IPasswordHasher>();
        return new LoginCommandHandler(users, hasher, new LoginCommandValidator());
    }

    [Fact]
    public async Task Succeeds_ForCorrectCredentials()
    {
        var sut = CreateSut(out var users, out var hasher);
        var user = new User { Id = Guid.NewGuid(), Email = "user@example.com", PasswordHash = "stored-hash" };
        users.GetByEmailAsync("user@example.com", Arg.Any<CancellationToken>()).Returns(user);
        hasher.Verify("correct-password", "stored-hash").Returns(true);

        var result = await sut.HandleAsync(
            new LoginCommand("user@example.com", "correct-password"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value!.Id);
    }

    [Fact]
    public async Task Fails_ForWrongPassword()
    {
        var sut = CreateSut(out var users, out var hasher);
        var user = new User { Id = Guid.NewGuid(), Email = "user@example.com", PasswordHash = "stored-hash" };
        users.GetByEmailAsync("user@example.com", Arg.Any<CancellationToken>()).Returns(user);
        hasher.Verify("wrong-password", "stored-hash").Returns(false);

        var result = await sut.HandleAsync(
            new LoginCommand("user@example.com", "wrong-password"), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Fails_ForUnknownEmail_WithSameErrorAsWrongPassword()
    {
        var sut = CreateSut(out var users, out _);
        users.GetByEmailAsync("nobody@example.com", Arg.Any<CancellationToken>()).Returns((User?)null);

        var unknownEmailResult = await sut.HandleAsync(
            new LoginCommand("nobody@example.com", "whatever"), CancellationToken.None);

        var sut2 = CreateSut(out var users2, out var hasher2);
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
