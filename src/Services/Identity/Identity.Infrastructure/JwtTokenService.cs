using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BuildingBlocks.Security;
using Identity.Application;
using Identity.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure;

/// <summary>
/// Issues the access/refresh pair every other service trusts (docs/Security-Model.md
/// §1) - uses the same JwtOptions (Issuer/Audience/SigningKey) that
/// BuildingBlocks.Security's bearer validation checks against, so a token issued
/// here validates everywhere else without any extra wiring.
/// </summary>
public class JwtTokenService : ITokenService
{
    private readonly JwtOptions _jwtOptions;
    private readonly TokenIssuanceOptions _issuanceOptions;

    public JwtTokenService(IOptions<JwtOptions> jwtOptions, IOptions<TokenIssuanceOptions> issuanceOptions)
    {
        _jwtOptions = jwtOptions.Value;
        _issuanceOptions = issuanceOptions.Value;
    }

    public Task<TokenPair> IssueAsync(User user, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var accessTokenExpiresAtUtc = now.Add(_issuanceOptions.AccessTokenLifetime);
        var refreshTokenExpiresAtUtc = now.Add(_issuanceOptions.RefreshTokenLifetime);

        var accessToken = CreateAccessToken(user, accessTokenExpiresAtUtc);
        var refreshToken = GenerateRefreshToken();

        return Task.FromResult(new TokenPair(accessToken, accessTokenExpiresAtUtc, refreshToken, refreshTokenExpiresAtUtc));
    }

    private string CreateAccessToken(User user, DateTime expiresAtUtc)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Roles currently double as permission strings - the domain model
        // (docs/Microservice-Responsibilities.md) has no separate permissions
        // concept yet. Emitted as both claim types so a standard role check
        // (ClaimTypes.Role) and BuildingBlocks.Security's permission-based
        // [RequirePermission] check (PermissionAuthorizationHandler.ClaimType)
        // both work against the same token without Identity knowing which one
        // a given downstream service uses.
        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim(PermissionAuthorizationHandler.ClaimType, role));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
