# Microservice Responsibilities

One section per service: what it owns, what it publishes, what it consumes. The rule enforced across this table is **no two services claim ownership of the same data** — every other service either calls the owning service's API for a real-time answer, or reacts to an event the owning service published. Nothing reads another service's database directly.

Event names use past tense per `Coding-Standards.md`'s naming convention; all are published via the outbox pattern (`BuildingBlocks.Messaging`, Phase 4) and consumed idempotently (`ProcessedEvents` table).

## Identity Service

**Owns:** `User` (email, password hash, roles), `RefreshToken` (token hash, expiry, revoked flag, replaced-by pointer for rotation tracking).

**Publishes:** identity/session lifecycle events as they're introduced in Phase 5 (e.g. a user-registered event, if another service ever needs to react to account creation — not yet finalized; add to this table when Phase 5 implementation decides it).

**Consumes:** nothing — Identity is a root service with no upstream dependency on another service's events.

**Called synchronously by:** the Gateway (login, token refresh, registration), and implicitly trusted by every other service via JWT validation (`BuildingBlocks.Security`) rather than a live call per request.

## Wallet Service

**Owns:** `Account` (balance, owner, currency), `LedgerEntry` (immutable, append-only — the source of truth every balance is computed from), with a `RowVersion` optimistic concurrency token on `Account`.

**Publishes:** `WalletDebited`, `WalletCredited` — emitted after a ledger entry is durably written, via outbox.

**Consumes:** nothing — Wallet doesn't react to other services' events; it only responds to direct calls.

**Called synchronously by:** the Payment service (`DebitAsync`/`CreditAsync`, with `Idempotency-Key` enforcement), and the Gateway (balance queries for the frontend).

## Payment Service

**Owns:** `Payment` (state machine: e.g. Created → Authorized → Captured / Failed, with explicit legal-transition guards).

**Publishes:** `PaymentCaptured`, `PaymentFailed` — emitted by the saga orchestrator as the payment reaches a terminal state, via outbox.

**Consumes:** nothing from the event bus — Payment's only cross-service dependency is the synchronous call to Wallet; it does not react to `WalletDebited`/`WalletCredited` asynchronously, because it needs the debit result synchronously to drive its own saga's next state transition.

**Called synchronously by:** the Gateway (`POST /payments`, with `Idempotency-Key` enforcement).

**Calls synchronously:** the Wallet service, wrapped in Polly retry/circuit-breaker policies, since Wallet's availability directly gates whether the saga can proceed.

## Notification Service

**Owns:** its own delivery/dedupe records (which event IDs have already produced a notification) — no business data from other services is duplicated here beyond what's needed for the notification content itself.

**Publishes:** nothing — Notification is a terminal consumer; nothing downstream reacts to a notification being sent.

**Consumes:** `PaymentCaptured`, `PaymentFailed`, and `WalletDebited`/`WalletCredited` where relevant, via a RabbitMQ consumer, using the idempotent-consumer pattern so at-least-once delivery never produces two notifications for the same event.

## Audit Service

**Owns:** `AuditEntry` — an append-only, hash-chained record for tamper evidence (Phase 9), independent of every other service's database.

**Publishes:** nothing — Audit is a pure sink.

**Consumes:** events from every other service (`PaymentCaptured`, `PaymentFailed`, `WalletDebited`, `WalletCredited`, and any Identity events added later), using the same idempotent-consumer pattern as Notification. Audit's completeness guarantee comes entirely from what crosses the event bus — it never reads another service's database directly, so a bug in another service's write path can't silently corrupt the audit trail.

## Gateway (not a domain service, included for completeness)

**Owns:** no business data — routing configuration, rate-limit state, and JWT validation only.

**Publishes/Consumes:** nothing on the event bus — the Gateway is purely synchronous, sitting between the Angular frontend and every service's HTTP API.

## Ownership Cross-Check

| Data | Owning Service | Who else touches it, and how |
|---|---|---|
| User credentials, refresh tokens | Identity | Everyone else validates JWTs issued by Identity; nobody else stores credentials |
| Account balance, ledger | Wallet | Payment calls Wallet synchronously; nobody else writes to the ledger |
| Payment state | Payment | Notification/Audit react to `PaymentCaptured`/`PaymentFailed` events; nobody else writes payment state |
| Notification delivery status | Notification | Not read by any other service |
| Audit trail | Audit | Not read synchronously by any other service; exists for independent verification |

No row above has two owners — this table is the concrete check against the Phase 1 Definition of Done requirement that "every service's responsibility is unambiguous."
