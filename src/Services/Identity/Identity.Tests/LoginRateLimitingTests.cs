using Identity.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Identity.Tests;

/// <summary>
/// Exercises the LoginRateLimiting policy in isolation against a minimal TestServer
/// endpoint, rather than the full Identity.Api host - avoids needing a live database
/// just to prove the rate limiter itself behaves (partition, permit limit, 429 shape).
/// </summary>
public class LoginRateLimitingTests : IAsyncLifetime
{
    private IHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRateLimiter(LoginRateLimiting.Configure);
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/api/v1/auth/login", () => Results.Ok())
                            .RequireRateLimiting(LoginRateLimiting.PolicyName);
                    });
                });
            })
            .StartAsync();

        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    [Fact]
    public async Task Requests_within_permit_limit_succeed()
    {
        for (var i = 0; i < LoginRateLimiting.PermitLimit; i++)
        {
            var response = await _client.GetAsync("/api/v1/auth/login");
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task Request_beyond_permit_limit_is_rejected_with_429()
    {
        for (var i = 0; i < LoginRateLimiting.PermitLimit; i++)
        {
            var response = await _client.GetAsync("/api/v1/auth/login");
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        }

        var rejected = await _client.GetAsync("/api/v1/auth/login");

        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
    }
}
