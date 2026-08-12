# Technology Decisions

This file is **not** a decision log. Individual technology and design decisions are recorded as numbered ADRs in `docs/adr/`, one file per decision, per `ADR-Template-and-Starter-Log.md` — that file is the source of truth for reasoning and trade-offs.

This file holds only two things:

1. The handful of decisions that are genuinely fixed *at Phase 1* — implied directly by the architecture itself, not warranting a standalone ADR.
2. An index of every ADR: number, title, phase, and status, kept current as ADRs are written.

## Phase 1 Decisions

These are structural choices baked into the architecture as described in [Architecture.md](Architecture.md), not independent technology picks — there isn't a real alternative being weighed here, so they don't get their own ADR.

- **Microservices over a monolith.** The platform is split into five independently deployable services (Identity, Wallet, Payment, Notification, Audit) rather than one deployable, because the project's explicit goal is to demonstrate microservice patterns (sagas, outbox, service-to-service resilience, per-service scaling) — a monolith wouldn't exercise any of that.
- **Monorepo.** All services, the Gateway, the Angular frontend, and shared libraries live in one repository (`enterprise-payment-platform`), scaffolded in Phase 2. This keeps cross-service changes (e.g. a shared `BuildingBlocks` library update) atomic and reviewable in a single PR, at the cost of a coarser-grained CI trigger surface — an acceptable trade-off for a solo project.
- **Clean Architecture layering per service.** Every service is structured as `Api / Application / Domain / Infrastructure / Tests`. This is mandated directly by the main instruction doc's coding standards, not a choice between alternatives.
- **Database-per-service (PostgreSQL).** Each service owns its own Postgres database; no service reads or writes another service's schema. This is a structural consequence of "services own their data exclusively" — the specific alternative (shared instance, shared schema) was never on the table given that constraint.

Anything with a real alternative to weigh — which message broker, which password hashing algorithm, which frontend state library, which gateway library, and so on — is deferred to its own ADR in the phase where it's actually decided. See the index below.

## ADR Index

| ADR # | Title | Phase | Status |
|---|---|---|---|
| ADR-0001 | Message Broker — RabbitMQ vs. Kafka | 2 | Accepted |
| ADR-0002 | Payment Saga — Orchestration vs. Choreography | 2 | Accepted |
| ADR-0003 | Monorepo vs. Polyrepo | 2 | Pending |
| ADR-0004 | Password Hashing Algorithm | 5 | Pending |
| ADR-0005 | JWT vs. Opaque Tokens + Introspection | 5 | Pending |
| ADR-0006 | Immutable Ledger vs. Mutable Balance Column | 6 | Pending |
| ADR-0007 | Optimistic vs. Pessimistic Concurrency Control | 6 | Pending |
| ADR-0008 | Append-Only Audit Table vs. Dedicated Event Store | 9 | Pending |
| ADR-0009 | State Management — NgRx vs. Angular Signals | 10 | Pending |
| ADR-0010 | Gateway — YARP vs. Ocelot | 10 | Pending |
| ADR-0011 | Namespace-per-Environment vs. Cluster-per-Environment | 12 | Pending |
| ADR-0012 | Secrets — Plain K8s Secrets vs. External-Secrets/Vault | 12 | Pending |
| ADR-0013 | Helm vs. Kustomize | 13 | Pending |
| ADR-0014 | Self-Hosted vs. GitHub-Hosted CI Runners | 14 | Pending |
| ADR-0015 | Tracing Backend — Tempo vs. Jaeger | 15 | Pending |
| ADR-0016 | Service Mesh / mTLS — Adopt Now or Defer | 16 | Pending |
| ADR-0017 | Load Testing Tool — k6 vs. Gatling vs. JMeter | 17 | Pending |
| ADR-0018 | Risk Service — Fail-Open vs. Fail-Closed on Outage | 18 | Pending |
| ADR-0019 | Reporting Service — Rebuild-on-Demand vs. Always-On Replica | 19 | Pending |

ADR-0001 and ADR-0002 are the two worked examples already written in `ADR-Template-and-Starter-Log.md` and are marked Accepted here to match. All other ADRs are written by the project owner, in phase order, per that file's own instruction — not drafted ahead of time.

Update this table's Status column as each ADR moves from Pending → Proposed → Accepted (or Deprecated/Superseded). Add new rows if a decision point is discovered mid-implementation that isn't already listed.
