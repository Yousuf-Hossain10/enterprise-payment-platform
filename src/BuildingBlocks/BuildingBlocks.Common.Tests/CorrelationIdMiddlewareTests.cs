using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Serilog;
using Serilog.Events;

namespace BuildingBlocks.Common.Tests;

public class CorrelationIdMiddlewareTests
{
    private static TestServer CreateServer(Action? onRequest = null)
    {
        var builder = new WebHostBuilder()
            .Configure(app =>
            {
                app.UseCorrelationId();
                app.Run(context =>
                {
                    onRequest?.Invoke();
                    return context.Response.WriteAsync("ok");
                });
            });

        return new TestServer(builder);
    }

    [Fact]
    public async Task GeneratesCorrelationId_WhenHeaderAbsent()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();

        var response = await client.GetAsync("/");

        Assert.True(response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values));
        Assert.True(Guid.TryParse(values!.Single(), out _));
    }

    [Fact]
    public async Task EchoesCorrelationId_WhenHeaderPresent()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "existing-correlation-id");

        var response = await client.GetAsync("/");

        var value = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();
        Assert.Equal("existing-correlation-id", value);
    }

    [Fact]
    public async Task SetsTraceIdentifier_ToCorrelationId()
    {
        string? observedTraceIdentifier = null;
        var builder = new WebHostBuilder()
            .Configure(app =>
            {
                app.UseCorrelationId();
                app.Run(context =>
                {
                    observedTraceIdentifier = context.TraceIdentifier;
                    return context.Response.WriteAsync("ok");
                });
            });
        using var server = new TestServer(builder);
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "trace-id-check");

        await client.GetAsync("/");

        Assert.Equal("trace-id-check", observedTraceIdentifier);
    }

    [Fact]
    public async Task PushesCorrelationId_IntoSerilogLogContext()
    {
        var events = new List<LogEvent>();
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(new DelegateSink(events.Add))
            .CreateLogger();
        try
        {
            using var server = CreateServer(() => Log.Information("handling request"));
            using var client = server.CreateClient();
            client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "log-context-check");

            await client.GetAsync("/");

            var emitted = Assert.Single(events);
            Assert.True(emitted.Properties.TryGetValue("CorrelationId", out var value));
            Assert.Equal("\"log-context-check\"", value!.ToString());
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private sealed class DelegateSink(Action<LogEvent> onEmit) : Serilog.Core.ILogEventSink
    {
        public void Emit(LogEvent logEvent) => onEmit(logEvent);
    }
}
