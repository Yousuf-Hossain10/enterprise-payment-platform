# Coding Standards

These are the rules every service, shared library, and the Gateway are held to, per the non-negotiable engineering rules in `Enterprise_Payment_Platform_Developer_Instruction.md`. They exist to keep a five-service, solo-authored codebase readable months later, not as process for its own sake.

## Clean Architecture Layering

Every service (`src/Services/<ServiceName>/`) is split into five projects, per `Folder-Structure.md`:

| Project | Contains | Depends on |
|---|---|---|
| `*.Domain` | Entities, value objects, domain events, business rules | Nothing — no framework, no EF Core, no HTTP |
| `*.Application` | Command/query handlers, use-case orchestration, `Result<T>` returns | `Domain` only |
| `*.Infrastructure` | EF Core `DbContext`, repository implementations, outbox dispatcher, message publisher, external HTTP clients | `Application`, `Domain` |
| `*.Api` | Controllers/minimal API endpoints, composition root (`Program.cs`), middleware wiring | `Application`, `Infrastructure` (for DI registration only) |
| `*.Tests` | Unit tests (`Domain`, `Application`) and integration tests (Testcontainers, per Phase 5+) | Whatever it's testing |

The dependency arrow always points inward — `Domain` never references `Application`, `Infrastructure`, or `Api`. If a domain entity needs something from the outside world (e.g. current time, a generated ID), it takes an abstraction as a constructor argument or method parameter, not a concrete infrastructure type.

## SOLID, Applied Concretely

- **Single Responsibility** — a handler does one use case. If a class needs "and" in its description, split it.
- **Open/Closed** — new payment states or risk rules are added by implementing an interface (`IRiskRule`, a new state transition), not by adding branches to existing logic.
- **Liskov Substitution** — any interface implementation must be swappable without the caller knowing (this is what makes Testcontainers-backed integration tests and in-memory fakes both viable against the same interfaces).
- **Interface Segregation** — prefer several small interfaces (`IWalletDebitClient`, `IWalletQueryClient`) over one fat one, when consumers only need part of the surface.
- **Dependency Inversion** — `Application` defines the interfaces it needs (`IAccountRepository`, `IEventPublisher`); `Infrastructure` implements them. Constructor injection everywhere; no service locator, no `new` of a concrete infrastructure type inside `Application` or `Domain` code.

## Mandated Patterns

These are specified directly by the main instruction doc and used consistently across all services:

- **Repository pattern** — `Application` depends on an `I<Entity>Repository` interface; `Infrastructure` provides the EF Core implementation. No `DbContext` is ever injected directly into a handler.
- **Options pattern / strongly-typed configuration** — every piece of configuration (connection strings, JWT settings, RabbitMQ credentials) is bound to a typed class via `IOptions<T>`, validated on startup (`.ValidateOnStart()`), not read via `IConfiguration["Key:SubKey"]` string lookups scattered through the code.
- **Global exception middleware + Problem Details** — every service returns RFC 7807-shaped error responses for unhandled exceptions, via the shared middleware in `BuildingBlocks.Common` (Phase 4, Day 11). Handlers don't wrap business logic in try/catch to produce error responses — expected failures return `Result<T>.Failure(...)`, and only truly unexpected exceptions reach the global handler.
- **`Result<T>` for expected failures** — handlers return `Result<T>` for anything that's a normal "no" (validation failure, insufficient balance, conflict), reserving exceptions for actually exceptional conditions. Controllers map `Result<T>` to the appropriate HTTP status.
- **Async/await and `CancellationToken`** — every I/O-bound method is `async` and accepts (and forwards) a `CancellationToken`, sourced from the request's `HttpContext.RequestAborted` or the consumer's cancellation source. No `.Result` / `.Wait()` blocking on async code, ever.
- **Idempotent consumers** — every RabbitMQ consumer checks a `ProcessedEventIds` table before acting, per the pattern in `BuildingBlocks.Messaging` (Phase 4, Day 14).

## Naming Conventions

- **Projects**: `<ServiceName>.<Layer>` (e.g. `Wallet.Application`), `BuildingBlocks.<Concern>` for shared libraries.
- **Commands/Queries**: verb-first, intent-revealing (`DebitAccountCommand`, `GetWalletBalanceQuery`), each with a matching `<Name>Handler`.
- **Domain events**: past tense (`WalletDebited`, `PaymentCaptured`, `RefreshTokenRevoked`) — an event describes something that already happened.
- **Interfaces**: `I`-prefixed, named for the capability, not the implementation (`IAccountRepository`, not `IEfAccountRepository`).
- **Async methods**: `Async` suffix (`DebitAsync`, `GetByIdAsync`).
- **No magic strings** — route templates, event type names, permission strings, and configuration keys are `const`/`static readonly` fields or enums, defined once and referenced everywhere else.
- **No duplicated logic** — if the same validation, mapping, or business rule shows up in two services, it either belongs in `BuildingBlocks` (if it's cross-cutting) or is a sign the service boundary needs a second look (if it's domain logic — see `Microservice-Responsibilities.md`).

## Testing Expectations

- Domain and Application logic gets unit tests with no infrastructure dependencies (fakes/mocks behind the interfaces above).
- Infrastructure gets integration tests against real dependencies via Testcontainers (real Postgres, real RabbitMQ) starting in Phase 5 — no mocking the database in tests that are meant to catch real infrastructure bugs.
- No compiler warnings, per the Engineering Standards in the main instruction doc (`dotnet build --warnaserror` gates CI from Phase 14 onward).

## Definition of Done for Any Code Change

Before any feature is considered complete (per the Production Readiness Checklist in the main instruction doc):

- [ ] Follows the layering and patterns above
- [ ] Unit tests (and integration tests, where infrastructure is involved) passing
- [ ] No compiler warnings
- [ ] Structured logging in place for the new code path
- [ ] Documentation updated if the change affects architecture, an API contract, or a service's responsibilities
