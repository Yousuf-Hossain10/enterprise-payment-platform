# BuildingBlocks.Common

Cross-cutting primitives every service references instead of reimplementing. See `docs/Coding-Standards.md` for the patterns this library exists to enforce.

Built across Phase 4, Days 11-12; feature-complete per the tutorial's spec as of Day 12.

## `Result<T>` (Day 11)

Handlers return `Result<T>` for expected failures (validation, business-rule rejection, conflict) instead of throwing for control flow — exceptions are reserved for truly unexpected conditions, which the Problem Details middleware below handles.

```csharp
public Result<Payment> CapturePayment(PaymentRequest request)
{
    if (request.Amount <= 0)
        return Result<Payment>.Failure("Amount must be positive.");

    // ...

    return Result<Payment>.Success(payment);
}
```

`Failure` rejects a null/empty/whitespace error message — a failure with no reason defeats the point of the type.

## `UseProblemDetailsExceptionHandler()` (Day 11)

An `IApplicationBuilder` extension, called once in each service's `Program.cs`, that catches unhandled exceptions and returns an RFC 7807 Problem Details response (`application/problem+json`), per `docs/API-Guidelines.md`.

```csharp
var app = builder.Build();
app.UseProblemDetailsExceptionHandler();
```

The exception's message is only included in the response body (`Detail`) when the host environment is `Development` — outside that, `Detail` is `null`, so internal exception messages are never leaked to a caller in staging/prod. `Instance` is set to `HttpContext.TraceIdentifier`, which is the correlation ID once the correlation-ID middleware below is wired in ahead of it.

## `UseCorrelationId()` (Day 12)

An `IApplicationBuilder` extension that reads `X-Correlation-Id` from the request (generating one if absent), sets it as `HttpContext.TraceIdentifier`, echoes it back on the response, and pushes it into Serilog's `LogContext` for the duration of the request — so every log line emitted while handling that request carries the same `CorrelationId` property, per `docs/Logging-Strategy.md`. **Must be registered before `UseProblemDetailsExceptionHandler()`** so error responses carry the same ID as everything else.

```csharp
var app = builder.Build();
app.UseCorrelationId();
app.UseProblemDetailsExceptionHandler();
```

## `IdempotentRequestValidatorBase<T>` (Day 12)

A base class for FluentValidation validators of any request that carries an `Idempotency-Key` (every financial write endpoint, per `docs/API-Guidelines.md`). Implement `IIdempotentRequest` and inherit from this instead of re-declaring the same `NotEmpty()` rule per service.

```csharp
public record CapturePaymentRequest(string IdempotencyKey, decimal Amount) : IIdempotentRequest;

public class CapturePaymentRequestValidator : IdempotentRequestValidatorBase<CapturePaymentRequest>
{
    public CapturePaymentRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}
```

## `AddValidatedOptions<TOptions>()` (Day 12)

An `IServiceCollection` extension that binds a configuration section to a typed options class and validates it via data annotations **on startup** (`ValidateOnStart()`), not on first access — per `docs/Coding-Standards.md`, a missing or invalid config value should crash the service at boot, not fail whichever request happens to touch it first.

```csharp
public class WalletDatabaseOptions
{
    [Required]
    public string? ConnectionString { get; set; }
}

builder.Services.AddValidatedOptions<WalletDatabaseOptions>("Wallet:Database");
```

## The Other Three Libraries

Built across the rest of Phase 4: the outbox pattern and idempotent-consumer helper (`../BuildingBlocks.Messaging/README.md`), OpenTelemetry tracing/metrics and health checks (`../BuildingBlocks.Observability/README.md`), and JWT middleware/permission attributes (`../BuildingBlocks.Security/README.md`). `../Ping.Api/README.md` proves all four compose and boot together.
