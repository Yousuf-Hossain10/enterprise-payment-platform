using System.IdentityModel.Tokens.Jwt;
using BuildingBlocks.Security;
using Identity.Domain;
using Identity.Infrastructure;
using Microsoft.Extensions.Options;

namespace Identity.Tests;

public class JwtTokenServiceTests
{
    private static readonly JwtOptions SampleJwtOptions = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "this-is-a-32-plus-character-test-signing-key"
    };

    private static JwtTokenService CreateSut(TokenIssuanceOptions? issuanceOptions = null) =>
        new(Options.Create(SampleJwtOptions), Options.Create(issuanceOptions ?? new TokenIssuanceOptions()));

    private static User SampleUser(params string[] roles) => new()
    {
        Id = Guid.NewGuid(),
        Email = "user@example.com",
        PasswordHash = "irrelevant",
        Roles = roles
    };

    [Fact]
    public async Task IssueAsync_ProducesAccessToken_WithExpectedClaims()
    {
        var sut = CreateSut();
        var user = SampleUser("wallet:debit");

        var tokens = await sut.IssueAsync(user, CancellationToken.None);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken);
        Assert.Equal("test-issuer", jwt.Issuer);
        Assert.Contains("test-audience", jwt.Audiences);
        Assert.Equal(user.Id.ToString(), jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(user.Email, jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Contains(jwt.Claims, c => c.Type == PermissionAuthorizationHandler.ClaimType && c.Value == "wallet:debit");
    }

    [Fact]
    public async Task IssueAsync_SetsAccessTokenExpiry_PerTokenIssuanceOptions()
    {
        var sut = CreateSut(new TokenIssuanceOptions { AccessTokenLifetime = TimeSpan.FromMinutes(15) });

        var before = DateTime.UtcNow;
        var tokens = await sut.IssueAsync(SampleUser(), CancellationToken.None);
        var after = DateTime.UtcNow;

        Assert.InRange(tokens.AccessTokenExpiresAtUtc, before.AddMinutes(15), after.AddMinutes(15).AddSeconds(1));
    }

    [Fact]
    public async Task IssueAsync_ProducesDifferentRefreshToken_EachCall()
    {
        var sut = CreateSut();
        var user = SampleUser();

        var first = await sut.IssueAsync(user, CancellationToken.None);
        var second = await sut.IssueAsync(user, CancellationToken.None);

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
    }
}
