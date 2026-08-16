using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace BuildingBlocks.Observability;

public static class OpenTelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Wires OpenTelemetry tracing + metrics once, per docs/Observability-Strategy.md
    /// ("OpenTelemetry is wired once, in the shared library, not per service").
    /// Traces export via OTLP (Tempo, Phase 15); metrics are scraped by Prometheus
    /// (installed Phase 3) at /metrics, mapped separately via
    /// <see cref="HealthCheckEndpointExtensions"/>'s sibling metrics endpoint.
    /// </summary>
    public static IServiceCollection AddPlatformObservability(this IServiceCollection services, string serviceName)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddPrometheusExporter());

        services.AddHealthChecks();

        return services;
    }
}
