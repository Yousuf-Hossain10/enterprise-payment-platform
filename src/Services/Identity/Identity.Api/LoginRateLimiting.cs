using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.Api;

/// <summary>
/// Brute-force protection on login, per docs/Security-Model.md §6/§7 - this is a
/// service-level stopgap. The Gateway doesn't exist yet (Phase 10) and its own
/// rate limiting on sensitive endpoints lands Phase 16 (Day 81); this policy is
/// the interim defense-in-depth layer until that centralized one exists, and
/// stays in place as a second layer afterward.
/// </summary>
public static class LoginRateLimiting
{
    public const string PolicyName = "LoginPolicy";
    public const int PermitLimit = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static void Configure(RateLimiterOptions options)
    {
        options.AddPolicy(PolicyName, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = PermitLimit,
                    Window = Window,
                    QueueLimit = 0
                }));

        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.HttpContext.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many login attempts",
                    Detail = $"Rate limit exceeded ({PermitLimit} attempts per {Window.TotalMinutes:0} minute). Try again later.",
                    Instance = context.HttpContext.TraceIdentifier
                },
                options: null,
                contentType: "application/problem+json",
                cancellationToken: cancellationToken);
        };
    }
}
