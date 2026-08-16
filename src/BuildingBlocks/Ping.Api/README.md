# Ping.Api

Throwaway service proving all four `BuildingBlocks.*` libraries compose and boot together — the Phase 4 Definition of Done ("A throwaway 'ping' service references all four and boots successfully").

Not a real deployable: it isn't in `docs/Folder-Structure.md`'s topology, has no business logic, and uses in-memory stand-ins (`InMemoryOutboxStore`, `InMemoryProcessedEventStore`) purely so `BuildingBlocks.Messaging`'s DI wiring has something to resolve — real services implement those against their own DbContext instead.

## Verified for real (Day 16)

Ran via `dotnet run`, then hit every composed endpoint:

- `GET /ping` → `200 {"message":"pong"}`
- `GET /health/live` → `200` (liveness, no dependency checks)
- `GET /health/ready` → `200` (readiness, vacuously healthy — no checks registered)
- `GET /metrics` → `200`, Prometheus exposition format

Every log line during that run carried both `CorrelationId` (`BuildingBlocks.Common`, Day 12) and `TraceId`/`SpanId` (`BuildingBlocks.Observability`, Day 15) — confirmed the four libraries aren't just independently buildable, they actually function together as one request pipeline.

Safe to delete once reviewed, or keep as a smoke-test harness for future `BuildingBlocks` changes — either is fine.
