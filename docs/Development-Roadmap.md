# Development Roadmap

The 19-phase build plan from `Enterprise_Payment_Platform_Tutorial.md` / `Sprint-Plan-Day-by-Day.md`, restated as a checklist. Check off a phase only once its Definition of Done (in the tutorial) is fully met — not just when the day-by-day task list is exhausted. This file is updated as phases complete; it is the single at-a-glance answer to "how far along is this project."

- [x] **Phase 1 — Architecture & Planning** *(Days 1–4)* — all 11 `/docs` documents, full Mermaid diagram set
- [ ] **Phase 2 — Repository Initialization** *(Days 5–6)* — monorepo scaffold, `dotnet build` + `ng build` both succeed
- [ ] **Phase 3 — Local Infrastructure** *(Days 7–10)* — `bootstrap.sh`/`.ps1` stands up the full local platform; `teardown.sh` cleanly tears it down
- [ ] **Phase 4 — Shared Backend Foundation Libraries** *(Days 11–16)* — `BuildingBlocks.Common`, `.Messaging`, `.Observability`, `.Security`, each with tests and a README; a throwaway "ping" service proves composition
- [ ] **Phase 5 — Identity Service** *(Days 17–22)* — registration/login, Argon2id hashing, JWT issuance, refresh rotation/revocation, Testcontainers integration tests, OpenAPI spec, login rate limiting
- [ ] **Phase 6 — Wallet Service** *(Days 23–30)* — ledger-based balance, optimistic concurrency, idempotent debit/credit, outbox events, parallel-debit concurrency test, reconciliation report
- [ ] **Phase 7 — Payment Service** *(Days 31–38)* — state machine, resilient Wallet client, capture saga, idempotency-key enforcement, fault-injection test, contract tests
- [ ] **Phase 8 — Notification Service** *(Days 39–42)* — RabbitMQ consumer, idempotent handling, DLQ + depth alert, duplicate-event test
- [ ] **Phase 9 — Audit Service** *(Days 43–46)* — append-only schema, cross-service event consumption, paginated query API, hash-chained tamper evidence, verified query performance at 50k+ rows
- [ ] **Phase 10 — Angular Frontend** *(Days 47–54)* — Gateway (YARP), app shell, login + silent refresh, wallet/payment views, signal-based state, Playwright happy-path e2e, accessibility pass
- [ ] **Phase 11 — Docker Packaging** *(Days 55–56)* — multi-stage non-root Dockerfiles for all services + frontend, Trivy scan clean, image size budget met
- [ ] **Phase 12 — Kubernetes Manifests** *(Days 57–62)* — Deployments/Services for all services, ConfigMaps/Secrets/Ingress, HPA + PDB, default-deny NetworkPolicies, `kubeconform` clean
- [ ] **Phase 13 — Helm Charts** *(Days 63–66)* — umbrella chart + subcharts, per-environment `values.yaml`, golden-file tests, `helm lint` + `ct lint` clean
- [ ] **Phase 14 — GitHub Actions CI/CD** *(Days 67–72)* — build/test, frontend, Docker+Trivy, Helm/kubeconform validation jobs, self-hosted runner, branch protection, manual-approval prod deploy with rehearsed rollback
- [ ] **Phase 15 — Observability Stack** *(Days 73–78)* — per-service and platform-wide RED dashboards, Loki correlation-ID verification, Tempo tracing wired end-to-end, Alertmanager rules with a verified test alert
- [ ] **Phase 16 — Security Hardening** *(Days 79–82)* — STRIDE threat models (Wallet/Payment/Identity), dependency + secrets scanning in CI, Gateway rate limiting, OWASP ZAP baseline scan triaged
- [ ] **Phase 17 — Comprehensive Testing & Documentation** *(Days 83–88)* — test pyramid documented, k6 load test meeting p95 SLA, chaos test (Wallet pod kill), Phase 1 docs refreshed to match final implementation, operational runbooks dry-run
- [ ] **Phase 18 — Fraud/Risk Service** *(Days 89–92, extension)* — rule-based risk engine, wired into the Payment saga, configurable fail-open/fail-closed, manual review queue, full decision-outcome test coverage
- [ ] **Phase 19 — Reporting/Analytics Service** *(Days 93–96, extension)* — CQRS read models, idempotent event-driven upserts, staleness-aware query API, verified rebuild-from-Audit-log

## Governing Rules (from `CLAUDE.md`, restated here for visibility)

- One day's tasks at a time; state the Day/Phase before starting each session.
- Commit directly to `main` through Phase 13 — no feature branches/PRs until Phase 14 adds CI to gate them.
- Autonomy within a phase, checkpoint (push + review) between phases.
- No phase is skipped ahead of its dependencies, and no phase is considered done until its tutorial Definition of Done is met, not just its day-by-day task list.
- ADRs (`ADR-Template-and-Starter-Log.md`) are written by the project owner, one per numbered decision point, in the phase where that decision is actually reached.
- A Learning Journal entry is written at the end of any day marked for one in `Sprint-Plan-Day-by-Day.md`.

## After Phase 19

Per `Sprint-Plan-Day-by-Day.md`: a closing retrospective in the Learning Journal — which ADR would be revisited with hindsight, which phase taught the most, what would be done differently starting over.
