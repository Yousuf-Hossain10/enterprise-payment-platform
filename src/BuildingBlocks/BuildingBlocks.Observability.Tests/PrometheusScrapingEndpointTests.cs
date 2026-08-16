using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Observability.Tests;

public class PrometheusScrapingEndpointTests
{
    [Fact]
    public async Task MetricsEndpoint_ReturnsPrometheusExpositionFormat()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddPlatformObservability("test-service");
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseOpenTelemetryPrometheusScrapingEndpoint();
            });

        using var server = new TestServer(builder);
        using var client = server.CreateClient();

        var response = await client.GetAsync("/metrics");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("# TYPE", body);
    }
}
