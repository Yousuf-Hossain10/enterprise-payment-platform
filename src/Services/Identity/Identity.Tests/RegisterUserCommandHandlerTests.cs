using FluentValidation;
using Identity.Application;
using Identity.Domain;
using NSubstitute;

namespace Identity.Tests;

public class RegisterUserCommandHandlerTests
{
    private static RegisterUserCommandHandler CreateSut(
        out IUserRepository users, out IPasswordHasher hasher)
    {
        users = Substitute.For<IUserRepository>();
        hasher = Substitute.For<IPasswordHasher>();
        return new RegisterUserCommandHandler(users, hasher, new RegisterUserCommandValidator());
    }

    [Fact]
    public async Task Succeeds_AndHashesPassword_ForNewEmail()
    {
        var sut = CreateSut(out var users, out var hasher);
        users.GetByEmailAsync("new@example.com", Arg.Any<CancellationToken>()).Returns((User?)null);
        hasher.Hash("a-strong-password-123").Returns("hashed-value");

        var result = await sut.HandleAsync(
            new RegisterUserCommand("new@example.com", "a-strong-password-123"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        await users.Received(1).AddAsync(
            Arg.Is<User>(u => u.Email == "new@example.com" && u.PasswordHash == "hashed-value"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_WhenEmailAlreadyRegistered()
    {
        var sut = CreateSut(out var users, out _);
        users.GetByEmailAsync("existing@example.com", Arg.Any<CancellationToken>())
            .Returns(new User { Id = Guid.NewGuid(), Email = "existing@example.com", PasswordHash = "x" });

        var result = await sut.HandleAsync(
            new RegisterUserCommand("existing@example.com", "a-strong-password-123"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        await users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("not-an-email", "a-strong-password-123")]
    [InlineData("valid@example.com", "short")]
    public async Task Fails_ValidationBeforeTouchingRepository_ForInvalidInput(string email, string password)
    {
        var sut = CreateSut(out var users, out _);

        var result = await sut.HandleAsync(new RegisterUserCommand(email, password), CancellationToken.None);

        Assert.False(result.IsSuccess);
        await users.DidNotReceiveWithAnyArgs().GetByEmailAsync(default!, default);
    }
}
