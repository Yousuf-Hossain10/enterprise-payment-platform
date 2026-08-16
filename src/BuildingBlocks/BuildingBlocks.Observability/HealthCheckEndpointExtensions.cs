using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BuildingBlocks.Observability;

public static class HealthCheckEndpointExtensions
{
    /// <summary>
    /// Tag a service-specific check (DB, broker, ...) with this to include it in
    /// readiness, per docs/Deployment-Strategy.md - a pod isn't "up" for traffic
    /// routing until its readiness endpoint reports healthy, including a real check
    /// against its dependencies. Liveness deliberately runs zero checks: "is the
    /// process alive" shouldn't fail because a downstream dependency is down.
    /// </summary>
    public const string ReadyTag = "ready";

    public static IEndpointRouteBuilder MapPlatformHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag)
        });

        return endpoints;
    }
}
