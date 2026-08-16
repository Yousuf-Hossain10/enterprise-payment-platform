# BuildingBlocks.Common

Cross-cutting primitives every service references instead of reimplementing. See `docs/Coding-Standards.md` for the patterns this library exists to enforce.

This library is built incrementally across Phase 4 (Days 11-12); this README covers what exists as of each day and is updated as pieces are added — not written once and left stale.

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

The exception's message is only included in the response body (`Detail`) when the host environment is `Development` — outside that, `Detail` is `null`, so internal exception messages are never leaked to a caller in staging/prod. `Instance` is set to `HttpContext.TraceIdentifier`, which becomes the correlation ID once the correlation-ID middleware (Day 12) is wired in.

## Coming in Day 12

Correlation-ID middleware, FluentValidation base validators, and strongly-typed configuration helpers.
