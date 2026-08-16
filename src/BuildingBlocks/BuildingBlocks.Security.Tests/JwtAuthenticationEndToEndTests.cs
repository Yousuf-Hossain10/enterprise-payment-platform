using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Security.Tests;

public class JwtAuthenticationEndToEndTests
{
    private const string Issuer = "test-issuer";
    private const string Audience = "test-audience";
    private const string SigningKey = "this-is-a-32-plus-character-test-signing-key";

    private static TestServer CreateServer()
    {
        var builder = new WebHostBuilder()
            .ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Jwt:SigningKey"] = SigningKey
            }))
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddPlatformJwtAuthentication();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/wallet/debit", () => Results.Ok())
                        .RequireAuthorization(new RequirePermissionAttribute("wallet:debit").Policy!);
                });
            });

        return new TestServer(builder);
    }

    private static string CreateToken(params Claim[] claims)
    {
        var handler = new JwtSecurityTokenHandler();
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return handler.WriteToken(token);
    }

    [Fact]
    public async Task ReturnsUnauthorized_WhenNoTokenProvided()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();

        var response = await client.GetAsync("/wallet/debit");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReturnsForbidden_WhenTokenLacksRequiredPermission()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();
        var token = CreateToken(new Claim(PermissionAuthorizationHandler.ClaimType, "wallet:read"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/wallet/debit");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReturnsOk_WhenTokenHasRequiredPermission()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();
        var token = CreateToken(new Claim(PermissionAuthorizationHandler.ClaimType, "wallet:debit"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/wallet/debit");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReturnsUnauthorized_WhenTokenSignedWithWrongKey()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();
        var handler = new JwtSecurityTokenHandler();
        var wrongKeyCredentials = new SigningCredentials(
            new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("a-completely-different-32-char-key")),
            SecurityAlgorithms.HmacSha256);
        var token = handler.WriteToken(new JwtSecurityToken(
            issuer: Issuer, audience: Audience,
            claims: [new Claim(PermissionAuthorizationHandler.ClaimType, "wallet:debit")],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: wrongKeyCredentials));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/wallet/debit");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
