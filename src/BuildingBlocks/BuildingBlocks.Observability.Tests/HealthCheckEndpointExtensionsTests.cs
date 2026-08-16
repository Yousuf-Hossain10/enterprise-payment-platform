using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BuildingBlocks.Observability.Tests;

public class HealthCheckEndpointExtensionsTests
{
    private static TestServer CreateServer(HealthStatus readyCheckStatus)
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddHealthChecks()
                    .AddCheck("dependency", () => new HealthCheckResult(readyCheckStatus), [HealthCheckEndpointExtensions.ReadyTag]);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapPlatformHealthChecks());
            });

        return new TestServer(builder);
    }

    [Fact]
    public async Task Liveness_IsHealthy_EvenWhenReadinessCheckIsUnhealthy()
    {
        using var server = CreateServer(HealthStatus.Unhealthy);
        using var client = server.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_ReflectsTaggedCheck_WhenHealthy()
    {
        using var server = CreateServer(HealthStatus.Healthy);
        using var client = server.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_ReflectsTaggedCheck_WhenUnhealthy()
    {
        using var server = CreateServer(HealthStatus.Unhealthy);
        using var client = server.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
