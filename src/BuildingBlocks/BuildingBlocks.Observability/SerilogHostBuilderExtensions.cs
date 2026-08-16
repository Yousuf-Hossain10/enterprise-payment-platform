using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Formatting.Json;

namespace BuildingBlocks.Observability;

public static class SerilogHostBuilderExtensions
{
    /// <summary>
    /// Structured JSON logging to stdout, per docs/Logging-Strategy.md - never plain
    /// interpolated strings, never a local log file (container-native: the cluster's
    /// log pipeline collects stdout into Loki). Enrich.FromLogContext() is what makes
    /// the correlation-ID middleware's LogContext.PushProperty (BuildingBlocks.Common,
    /// Day 12) actually reach emitted log events.
    /// </summary>
    public static IHostBuilder UsePlatformSerilog(this IHostBuilder hostBuilder) =>
        hostBuilder.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console(new JsonFormatter(renderMessage: true)));
}
