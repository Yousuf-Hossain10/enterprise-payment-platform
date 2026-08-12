# API Guidelines

Conventions every service's `Api` layer and the Gateway follow, so a client (the Angular frontend, or a future integrator) sees one consistent API surface across five independently-built services.

## Versioning

- All routes are versioned in the URL path: `/api/v1/...`. No unversioned routes.
- A breaking change to a route's request/response shape ships as `/api/v2/...` alongside the still-supported `v1` route, rather than mutating `v1` in place. Given this is a solo project with one consumer (the Angular app) that's always redeployed together with the backend, `v2` routes are only expected to appear if a phase deliberately exercises the versioning story — not proactively.
- Version lives in the path, not a header or query string, so it's visible in logs, traces, and Gateway routing rules without extra parsing.

## Resource & Route Conventions

- Routes are noun-based and plural (`/api/v1/payments`, `/api/v1/accounts/{id}/ledger-entries`), verbs live in the HTTP method, not the path.
- Standard REST semantics: `GET` (read, no side effects), `POST` (create), `PUT`/`PATCH` (update), `DELETE` (remove) — financial state transitions that aren't a plain CRUD create (e.g. capturing a payment) are still modeled as a `POST` to a sub-resource or action route (`POST /api/v1/payments/{id}/capture`), not a generic `PATCH` with a status field, so the audit trail and API surface stay self-describing.
- Every request and response body uses `camelCase` JSON, consistent with the Angular frontend's native serialization expectations.

## Error Response Shape — RFC 7807 Problem Details

Every non-2xx response (across all services and the Gateway) is a Problem Details object, produced by the shared global exception middleware in `BuildingBlocks.Common` (Phase 4, Day 11):

```json
{
  "type": "https://payment-platform.dev/errors/insufficient-balance",
  "title": "Insufficient balance",
  "status": 409,
  "detail": "Account 3f2a... has insufficient balance for a debit of 50.00 USD.",
  "instance": "/api/v1/wallet/accounts/3f2a.../debit",
  "traceId": "00-4bf92f...-00f067aa...-01"
}
```

- `type` is a stable, documented identifier for the error class (not just the HTTP status), so a client can branch on it without string-matching `detail`.
- `traceId` always matches the correlation ID for the request (see `Logging-Strategy.md`), so a failed response can be traced directly to the corresponding trace/log entries.
- Validation failures (400s) extend Problem Details with a standard `errors` dictionary (field name → array of messages), per ASP.NET Core's built-in `ValidationProblemDetails`, so FluentValidation failures don't need a bespoke shape.

## Idempotency

- Every financial write endpoint (`POST /api/v1/payments`, wallet debit/credit) **requires** an `Idempotency-Key` header. A missing header on these routes is itself a 400, not an implied "generate one for me."
- The same key replayed against the same endpoint and body returns the original result (same status, same body) rather than reprocessing — enforced server-side via the idempotency-key check on the underlying ledger/payment write, not just a client-side convention.
- A given idempotency key is scoped to one endpoint + one caller; it is not a global dedupe token across the whole API.
- Non-financial `GET`/read endpoints don't require idempotency keys — they're naturally idempotent.

## Correlation & Tracing

- Every request carries an `X-Correlation-Id` header (generated at the Gateway if absent, propagated unchanged through every downstream call). See `Logging-Strategy.md` for the full propagation contract.

## Authentication

- Every route except Identity's `login`/`register`/`refresh` requires a valid JWT bearer token, validated by the shared `BuildingBlocks.Security` middleware.
- Permission checks use the `[RequirePermission("wallet:debit")]`-style claims-based attribute (`Coding-Standards.md`) rather than ad hoc role checks scattered in handlers.

## Pagination

- List endpoints (e.g. Audit's query API, Phase 9) use cursor-based pagination (`?cursor=...&limit=50`), not offset-based, since audit/ledger data is append-only and offset pagination drifts under concurrent writes.
- Paginated responses include `nextCursor` (null when exhausted) alongside the `items` array — no separate "total count" field is guaranteed, since computing an exact count over a large append-only table isn't free and isn't needed for cursor pagination to work.

## OpenAPI

- Every service publishes an OpenAPI/Swagger spec for its `v1` API (Identity's is due explicitly in Phase 5, Day 21; the same expectation applies to every other service as it's built). The spec is the source of truth for request/response shapes — hand-written API docs are not maintained separately.
