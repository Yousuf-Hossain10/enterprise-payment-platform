# Observability Strategy

## The Three Pillars

| Pillar | Tool | Owns |
|---|---|---|
| **Metrics** | Prometheus (collection) + Grafana (dashboards) | Request rate/error/duration (RED method) per service, RabbitMQ queue depth, Postgres connection pool saturation, JVM/.NET runtime metrics |
| **Logs** | Loki (storage/query) + Grafana (UI) | Structured, correlation-ID-tagged application logs — see `Logging-Strategy.md` |
| **Traces** | Tempo (storage) + OpenTelemetry (instrumentation) + Grafana (UI) | Distributed request traces spanning Gateway → services → async consumers |

All three are queried through **Grafana** as the single pane of glass — no separate tool per pillar for day-to-day investigation. This stack is installed incrementally: Prometheus/Grafana/Loki in Phase 3 (local infra bootstrap), Tempo in Phase 15 (Day 76), once there's enough cross-service traffic for tracing to be worth wiring up.

## Instrumentation

- Every service gets metrics, health checks, and trace instrumentation "for free" by referencing `BuildingBlocks.Observability` (Phase 4, Day 15) — OpenTelemetry is wired **once**, in the shared library, not per service.
- Traces propagate across service boundaries automatically via OpenTelemetry's context propagation over the same HTTP calls and RabbitMQ message headers that already carry the correlation ID — the two aren't separate mechanisms bolted together, they travel the same path.
- Every service exposes liveness and readiness health-check endpoints (shared base class, same library) — liveness answers "is the process alive," readiness answers "can this pod actually serve traffic right now" (e.g. its DB connection is up), and only readiness gates whether the Kubernetes Service routes traffic to that pod.

## Dashboards

- **Per-service RED dashboards** (Phase 15, Day 73) — Rate, Errors, Duration for each of the five services plus the Gateway, so a single dashboard per service answers "is this thing healthy right now."
- **Platform-wide dashboard** (Phase 15, Day 74) — aggregate RED across all services plus RabbitMQ and Postgres saturation metrics, for an at-a-glance system health view.
- **Tracing view** (Phase 15, Day 77) — verifying a single trace spans the full payment flow end-to-end (Frontend → Gateway → Payment → Wallet → Notification) is an explicit Definition of Done item for that phase, not just "traces exist somewhere."

## Alerting

- Alertmanager rules (Phase 15, Day 78) cover: elevated error rate per service, latency SLO violations (p95/p99 against the SLOs below), RabbitMQ dead-letter queue depth growing, and Postgres connection pool saturation.
- A test alert is deliberately triggered as part of Phase 15's Definition of Done, to prove the alerting path (not just the dashboards) actually works end-to-end.

## SLOs Per Service

Initial targets — set conservatively at Phase 1 and revisited once real load-test data exists (Phase 17, Days 84–85), since setting an SLO before any load test has run is a starting hypothesis, not a measured commitment:

| Service | Availability target | Latency target (p95) | Notes |
|---|---|---|---|
| Identity | 99.5% | < 200ms (login/refresh) | Gates every other request via JWT validation, but validation itself is local (no per-request call to Identity) |
| Wallet | 99.9% | < 150ms (debit/credit) | Highest-rigor service; tightest latency target since Payment's saga blocks on it synchronously |
| Payment | 99.5% | < 300ms (capture, end-to-end saga) | The target explicitly exercised by the Phase 17 k6 load test |
| Notification | 99% | < 2s (event → notification sent) | Async, no synchronous caller waiting on it |
| Audit | 99% | < 500ms (query API) | Write path is async/best-effort by nature (event consumption); only the query API has a real latency expectation |

These numbers are placeholders sized for "reasonable defaults for a payment platform simulation," not derived from any measurement yet — they exist so Phase 15's alerting rules and Phase 17's load test have a concrete target to check against, and both phases are expected to tune them with real data rather than treat them as fixed forever.
