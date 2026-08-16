using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace BuildingBlocks.Observability.Tests;

public class OpenTelemetryServiceCollectionExtensionsTests
{
    [Fact]
    public void RegistersResolvableTracerAndMeterProviders()
    {
        var services = new ServiceCollection();

        services.AddPlatformObservability("test-service");
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<TracerProvider>());
        Assert.NotNull(provider.GetRequiredService<MeterProvider>());
    }

    [Fact]
    public void RegistersHealthCheckService()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddPlatformObservability("test-service");
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>());
    }
}
