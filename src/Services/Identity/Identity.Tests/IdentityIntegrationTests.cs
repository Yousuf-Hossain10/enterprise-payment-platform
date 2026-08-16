using BuildingBlocks.Security;
using Identity.Application;
using Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace Identity.Tests;

/// <summary>
/// Exercises the real Application handlers against real Infrastructure
/// implementations (Argon2id, EF Core/Npgsql, JWT) and a real, throwaway
/// Postgres container - per docs/Coding-Standards.md, "token rotation logic
/// is exactly the kind of thing that looks correct against an in-memory
/// provider and breaks against real constraints" (tutorial, Phase 5). No
/// mocks anywhere in this file; the unit tests elsewhere in this project
/// cover handler logic in isolation.
/// </summary>
public class IdentityIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    private IdentityDbContext _db = default!;
    private RegisterUserCommandHandler _registerHandler = default!;
    private LoginCommandHandler _loginHandler = default!;
    private RefreshCommandHandler _refreshHandler = default!;
    private RevokeCommandHandler _revokeHandler = default!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        _db = new IdentityDbContext(options);
        await _db.Database.MigrateAsync();

        var users = new UserRepository(_db);
        var refreshTokens = new RefreshTokenRepository(_db);
        var passwordHasher = new Argon2idPasswordHasher();
        var refreshTokenHasher = new Sha256RefreshTokenHasher();
        var tokenService = new JwtTokenService(
            Options.Create(new JwtOptions
            {
                Issuer = "integration-test",
                Audience = "integration-test",
                SigningKey = "integration-test-signing-key-at-least-32-chars"
            }),
            Options.Create(new TokenIssuanceOptions()));

        _registerHandler = new RegisterUserCommandHandler(users, passwordHasher, new RegisterUserCommandValidator());
        _loginHandler = new LoginCommandHandler(
            users, passwordHasher, new LoginCommandValidator(), tokenService, refreshTokens, refreshTokenHasher);
        _refreshHandler = new RefreshCommandHandler(
            refreshTokens, refreshTokenHasher, users, tokenService, new RefreshCommandValidator());
        _revokeHandler = new RevokeCommandHandler(refreshTokens, refreshTokenHasher, new RevokeCommandValidator());
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task FullFlow_RegisterLoginRefreshReuseLogout_WorksAgainstRealPostgres()
    {
        var register = await _registerHandler.HandleAsync(
            new RegisterUserCommand("integration@example.com", "a-strong-password-123"), CancellationToken.None);
        Assert.True(register.IsSuccess);

        var login = await _loginHandler.HandleAsync(
            new LoginCommand("integration@example.com", "a-strong-password-123"), CancellationToken.None);
        Assert.True(login.IsSuccess);

        var refresh = await _refreshHandler.HandleAsync(
            new RefreshCommand(login.Value!.RefreshToken), CancellationToken.None);
        Assert.True(refresh.IsSuccess);
        Assert.NotEqual(login.Value.RefreshToken, refresh.Value!.RefreshToken);

        var reuseOfRotatedToken = await _refreshHandler.HandleAsync(
            new RefreshCommand(login.Value.RefreshToken), CancellationToken.None);
        Assert.False(reuseOfRotatedToken.IsSuccess);

        var logout = await _revokeHandler.HandleAsync(
            new RevokeCommand(refresh.Value.RefreshToken), CancellationToken.None);
        Assert.True(logout.IsSuccess);

        var refreshAfterLogout = await _refreshHandler.HandleAsync(
            new RefreshCommand(refresh.Value.RefreshToken), CancellationToken.None);
        Assert.False(refreshAfterLogout.IsSuccess);
    }

    [Fact]
    public async Task Register_RejectsDuplicateEmail_EnforcedByRealUniqueIndex()
    {
        var first = await _registerHandler.HandleAsync(
            new RegisterUserCommand("duplicate@example.com", "a-strong-password-123"), CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await _registerHandler.HandleAsync(
            new RegisterUserCommand("duplicate@example.com", "a-different-password-456"), CancellationToken.None);

        Assert.False(second.IsSuccess);
    }

    [Fact]
    public async Task Login_RejectsWrongPassword_AgainstRealArgon2idHashInRealDatabase()
    {
        await _registerHandler.HandleAsync(
            new RegisterUserCommand("wrongpass@example.com", "the-correct-password-123"), CancellationToken.None);

        var result = await _loginHandler.HandleAsync(
            new LoginCommand("wrongpass@example.com", "not-the-correct-password"), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
