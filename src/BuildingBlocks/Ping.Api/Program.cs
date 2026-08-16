using BuildingBlocks.Common;
using BuildingBlocks.Messaging;
using BuildingBlocks.Observability;
using BuildingBlocks.Security;
using Ping.Api;

// Throwaway service proving the four BuildingBlocks libraries compose and boot
// together - the Phase 4 Definition of Done. Not a real deployable: it isn't
// listed in docs/Folder-Structure.md, has no service-specific logic, and is
// safe to delete once the composition is proven (or keep as a smoke-test
// harness for future BuildingBlocks changes - either is fine).
var builder = WebApplication.CreateBuilder(args);

builder.Host.UsePlatformSerilog();

builder.Services.AddPlatformObservability(serviceName: "ping-service");
builder.Services.AddPlatformJwtAuthentication();

builder.Services.AddOutboxDispatcher();
builder.Services.AddScoped<IOutboxStore, InMemoryOutboxStore>();

builder.Services.AddIdempotentEventConsumer();
builder.Services.AddScoped<IProcessedEventStore, InMemoryProcessedEventStore>();

var app = builder.Build();

app.UseCorrelationId();
app.UseProblemDetailsExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapPlatformHealthChecks();
app.MapGet("/ping", () => Results.Ok(new { message = "pong" }));
app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.Run();
