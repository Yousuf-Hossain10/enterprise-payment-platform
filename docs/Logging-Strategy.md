# Logging Strategy

## Structured Logging

- Every service logs via Serilog (wired once in `BuildingBlocks.Observability`, Phase 4, Day 15) emitting **structured JSON**, never plain interpolated strings — every log statement is a set of named properties (`{AccountId}`, `{PaymentId}`, `{CorrelationId}`), not a formatted sentence with values baked in. This is what makes the logs queryable in Loki by field rather than by regex over free text.
- Log output goes to stdout/stderr (container-native), collected by the cluster's log pipeline into Loki (Phase 3 install, Phase 15 dashboards/verification) — no service writes to a local log file.

## Correlation-ID Propagation

- Every inbound request either carries an `X-Correlation-Id` header or has one generated at the Gateway if absent.
- The correlation-ID middleware (`BuildingBlocks.Common`, Phase 4, Day 12) pushes the ID into Serilog's `LogContext` at the start of the request pipeline, so **every** log line emitted while handling that request — across every layer, without each call site passing it explicitly — carries the same `CorrelationId` property.
- The ID is forwarded unchanged on every outbound call the request triggers: Payment → Wallet's HTTP call carries the same header; a published RabbitMQ event carries the correlation ID in its payload/headers so the consuming service (Notification, Audit) continues the same `CorrelationId` in its own logs.
- This is what makes it possible to take one `CorrelationId` and pull every log line across all five services for a single user-facing request or saga — verified explicitly in Phase 15 (Day 75: "Loki correlation-ID query verification across all services").
- The correlation ID is also returned to the caller (e.g. in the Problem Details `instance`/`traceId` field, per `API-Guidelines.md`) so a bug report can be tied directly back to a log query.

## Log Levels — What Gets Logged Where

| Level | Used for | Example |
|---|---|---|
| **Trace/Debug** | Local dev only, verbose payloads, disabled by default in staging/prod (`Deployment-Strategy.md`) | Full request/response bodies, SQL parameter values |
| **Information** | Normal business events worth knowing happened, at production log volume | "Payment {PaymentId} captured", "Account {AccountId} debited {Amount}" |
| **Warning** | Recoverable/expected failure paths — a retry, a validation rejection, a circuit breaker opening | "Wallet call failed, retrying (attempt {Attempt})", "Idempotency key replay detected" |
| **Error** | An operation failed and did not recover — the global exception middleware logs at this level before returning a Problem Details response | Unhandled exception in a handler, a saga step that couldn't complete after exhausting retries |
| **Fatal** | The process cannot continue — reserved for startup failures (e.g. can't reach the database at boot) | Failed DI container validation, missing required configuration on `.ValidateOnStart()` |

Business-significant events (money movement, payment state transitions, auth failures) are always logged at **Information** or above — never at Debug — because Information-level logs are what's actually retained and queried in staging/prod.

## What Never Gets Logged

- Raw passwords, password hashes, JWT signing keys, or full JWT tokens.
- Full credit-card-shaped or other sensitive-looking payloads, even though this platform doesn't process real payment data (`Security-Model.md` §5) — logging hygiene doesn't relax just because the data is simulated, since the habit is the point.
- Full connection strings (credentials redacted if the connection string itself needs to appear in a diagnostic log).

## Ownership

- Loki is the query/storage backend for logs (installed Phase 3, dashboards/verification Phase 15). Grafana is the query UI — logs are never queried by `kubectl logs`-grepping in normal operation, only as a last-resort fallback if the pipeline itself is down.
