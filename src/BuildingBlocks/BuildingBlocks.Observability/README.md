# BuildingBlocks.Observability

The three observability pillars (`docs/Observability-Strategy.md`), wired once here so every service gets them by referencing this package — not configured per service.

## `AddPlatformObservability(serviceName)` (Day 15)

Wires OpenTelemetry tracing and metrics:

- **Tracing:** ASP.NET Core + HttpClient + Npgsql instrumentation, exported via OTLP (Tempo, Phase 15).
- **Metrics:** ASP.NET Core + .NET runtime instrumentation, exposed via the Prometheus exporter at `/metrics` (mapped separately — see below — since exposing it is an endpoint concern, not a DI concern).
- Also registers the health check service (`services.AddHealthChecks()`), so `MapPlatformHealthChecks()` works even before any service adds its own checks.

```csharp
builder.Services.AddPlatformObservability(serviceName: "wallet-service");
```

## `UsePlatformSerilog()` (Day 15)

Structured JSON logging to stdout, per `docs/Logging-Strategy.md` — never plain interpolated strings, never a local log file. `Enrich.FromLogContext()` is what makes `BuildingBlocks.Common`'s correlation-ID middleware (`LogContext.PushProperty`, Day 12) actually reach emitted log events.

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.UsePlatformSerilog();
```

## `MapPlatformHealthChecks()` + Prometheus scraping (Day 15)

Liveness/readiness convention, per `docs/Deployment-Strategy.md` — a pod isn't "up" for traffic routing until its readiness endpoint reports healthy, including a real check against its dependencies:

- **`/health/live`** — runs zero checks. "Is the process alive" shouldn't fail because a downstream dependency is down.
- **`/health/ready`** — runs every check tagged `HealthCheckEndpointExtensions.ReadyTag`. Each service adds its own DB/broker checks with this tag; this library has no opinion on what those checks are.

```csharp
var app = builder.Build();
app.UseRouting();
app.UseEndpoints(endpoints => endpoints.MapPlatformHealthChecks());
app.UseOpenTelemetryPrometheusScrapingEndpoint(); // exposes /metrics for Prometheus (installed Phase 3) to scrape

// per service, in Infrastructure:
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, tags: [HealthCheckEndpointExtensions.ReadyTag]);
```

**Testing note:** OpenTelemetry/health-check wiring is verified by resolving `TracerProvider`/`MeterProvider`/`HealthCheckService` from a real `ServiceProvider` (misconfiguration here typically throws at resolve time, so this is a meaningful smoke test), the health endpoints are exercised end-to-end via `TestServer` with fake tagged checks, and the `/metrics` endpoint is asserted to actually return Prometheus exposition-format text — all without any live collector, Prometheus server, or database, per `docs/Coding-Standards.md`'s testing strategy.

## Coming in Day 16

`BuildingBlocks.Security` (JWT middleware, permission attribute), READMEs for all four libraries reviewed together, and the throwaway "ping" service proving all four compose and boot successfully — the Phase 4 Definition of Done.
