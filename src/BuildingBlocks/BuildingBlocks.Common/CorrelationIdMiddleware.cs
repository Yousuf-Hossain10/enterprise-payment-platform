using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Serilog.Context;

namespace BuildingBlocks.Common;

public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context.Request.Headers);

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(IHeaderDictionary headers)
    {
        return headers.TryGetValue(HeaderName, out var values) && !StringValues.IsNullOrEmpty(values)
            ? values.ToString()
            : Guid.NewGuid().ToString();
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>
    /// Must run before <see cref="ProblemDetailsMiddlewareExtensions.UseProblemDetailsExceptionHandler"/> so
    /// error responses carry the same correlation ID as everything else in the request.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
