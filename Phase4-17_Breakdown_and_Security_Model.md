# Addendum — Expanded Phase 4–17 Breakdown & Security-Model.md Draft

*Companion to `Enterprise_Payment_Platform_Developer_Instruction.md`*

This addendum splits the compressed "PHASES 4–17" section into individually scoped phases (each with goal, deliverables, and Definition of Done), consistent with how Phases 1–3 were already specified, and drafts a fuller outline for `Security-Model.md`. Insert these into the main instruction document, replacing the current "PHASES 4–17" block.

---

## PHASE 4 — Shared Backend Foundation Libraries

**Goal:** Establish common .NET 8 building blocks so every microservice starts from the same primitives instead of reinventing cross-cutting concerns.

**Deliverables:**
- `BuildingBlocks.Common` — Result/Error pattern, Problem Details middleware, correlation-ID propagation, base DTO validation (FluentValidation)
- `BuildingBlocks.Messaging` — RabbitMQ abstraction with outbox pattern support and consumer idempotency helpers
- `BuildingBlocks.Observability` — OpenTelemetry setup (traces, metrics), structured logging (Serilog), health-check base classes
- `BuildingBlocks.Security` — JWT validation middleware, claims-based authorization helpers

**Definition of Done:** each library is an independently versioned internal NuGet package, has unit tests, and a README explaining its contract.

---

## PHASE 5 — Identity Service

**Goal:** Authentication and authorization for the whole platform.

**Deliverables:** registration/login, password hashing (Argon2id or BCrypt), JWT access + refresh token issuance, refresh-token rotation and revocation, role-based claims, OpenAPI spec, EF Core migrations.

**Definition of Done:** integration tests using Testcontainers (real Postgres), health/metrics/logging wired via shared libraries, Docker image builds, a short threat-model note referencing OWASP ASVS for authentication.

---

## PHASE 6 — Wallet Service

**Goal:** Correct, auditable money movement — this is the highest-risk service in the platform and deserves the most rigor.

**Deliverables:**
- Double-entry ledger schema (`accounts`, `ledger_entries`) — balance is *computed* from ledger rows, never a mutable field
- Optimistic concurrency control (row version) on account writes
- Idempotency-key table so repeated debit/credit calls are safe to retry
- Outbox pattern for publishing `WalletDebited` / `WalletCredited` events

**Definition of Done:** a concurrency test that fires parallel debits against the same account and proves no lost updates; a reconciliation report that sums ledger entries and matches account balances.

---

## PHASE 7 — Payment Service

**Goal:** Orchestrate the payment lifecycle across Wallet and Notification without losing consistency when a downstream call fails.

**Deliverables:** payment state machine (`Created → Authorized → Captured → Settled/Failed`), a saga (orchestration or choreography — pick one and document why) coordinating Wallet debit and Notification, mandatory `Idempotency-Key` header on write endpoints, Polly-based retry/circuit-breaker policies, dead-letter queue handling for events that repeatedly fail.

**Definition of Done:** a fault-injection test that kills the Wallet service mid-saga and verifies the Payment service recovers or compensates correctly; contract tests against Wallet and Notification APIs.

---

## PHASE 8 — Notification Service

**Goal:** Reliable, non-duplicating delivery of payment/wallet events as notifications.

**Deliverables:** RabbitMQ consumer, template engine for mocked email/SMS, retry policy, delivery audit trail forwarded to the Audit Service.

**Definition of Done:** consumer deduplicates by event ID (idempotent even under at-least-once delivery), DLQ is monitored with an alert.

---

## PHASE 9 — Audit Service

**Goal:** Immutable system-of-record for what happened across every service.

**Deliverables:** append-only audit log consuming domain events from all other services, paginated/filterable query API, documented retention policy. Optional stretch: hash-chaining entries for tamper evidence.

**Definition of Done:** query performance validated under realistic event volume; retention/deletion policy documented even if not yet enforced.

---

## PHASE 10 — Angular Frontend

**Goal:** A single, coherent client experience backed by a gateway rather than five separate origins.

**Deliverables:** API Gateway/BFF (YARP or Ocelot) in front of all services, feature-module structure, state management (NgRx or signals — pick one), auth interceptor handling JWT + silent refresh, Playwright/Cypress e2e suite covering the happy path, basic WCAG AA accessibility pass.

**Definition of Done:** e2e happy-path suite green in CI; Lighthouse score above an agreed threshold.

---

## PHASE 11 — Docker Packaging

**Goal:** Small, safe images.

**Deliverables:** multi-stage Dockerfiles per service, non-root user, minimal base image (alpine/distroless), image scanning (Trivy) integrated into the build.

**Definition of Done:** Trivy reports no unresolved critical CVEs; each image stays under an agreed size budget.

---

## PHASE 12 — Kubernetes Manifests

**Goal:** Declarative, least-privilege deployment definitions.

**Deliverables:** Deployments, Services, Ingress, ConfigMaps, Secrets (via sealed-secrets or external-secrets rather than raw K8s Secrets), HorizontalPodAutoscaler, PodDisruptionBudget, resource requests/limits, NetworkPolicies restricting service-to-service traffic.

**Definition of Done:** manifests pass `kubeconform`; apply cleanly to the Kind cluster from Phase 3.

---

## PHASE 13 — Helm Charts

**Goal:** Repeatable, environment-aware packaging of the manifests from Phase 12.

**Deliverables:** umbrella chart with per-service subcharts, `values.yaml` per environment (dev/staging/prod), golden-file tests via `helm template`.

**Definition of Done:** `helm lint` reports zero errors; chart-testing (`ct`) passes.

---

## PHASE 14 — GitHub Actions CI/CD

**Goal:** Automated build/test/scan/deploy pipeline matching the PR checklist already defined in the main document.

**Deliverables:** build/test/lint/scan stages, container build+push, Helm-based deploy stage with a manual approval gate before any "prod" environment, self-hosted runner setup documented, branch protection rules documented.

**Definition of Done:** pipeline runs green end-to-end on a sample PR; a documented rollback procedure exists and has been exercised once.

---

## PHASE 15 — Observability Stack

**Goal:** Make the running system explainable, not just monitored.

**Deliverables:** Prometheus scrape configs and Grafana RED-method dashboards per service, Loki log pipeline searchable by correlation ID, **distributed tracing (OpenTelemetry → Tempo or Jaeger)** wired through every service — this is currently missing from the main plan despite it listing sequence diagrams for multi-service flows — and Alertmanager rules for error-rate/latency SLOs.

**Definition of Done:** a single trace is visible end-to-end across a full payment flow (Frontend → Gateway → Payment → Wallet → Notification); a test alert fires correctly.

---

## PHASE 16 — Security Hardening

**Goal:** Close the gaps a "production-inspired" label implies.

**Deliverables:** lightweight STRIDE threat model per service, dependency scanning (Dependabot or Snyk) in CI, secrets scanning in CI, rate limiting at the gateway, optional mTLS between services (service mesh is a reasonable stretch goal, not a requirement), a self-run OWASP ZAP baseline scan.

**Definition of Done:** ZAP baseline report has no unresolved high-severity findings.

---

## PHASE 17 — Comprehensive Testing & Documentation

**Goal:** Close the loop — prove the system behaves under load and failure, and leave it operable by someone other than the original author.

**Deliverables:** documented test pyramid (unit/integration/contract/e2e ratios), load testing (k6) against the payment endpoint, a chaos test (kill a pod, verify recovery), refreshed architecture docs, operational runbooks (on-call, incident response, backup/restore).

**Definition of Done:** load test meets an agreed SLA (e.g., p95 < 300ms at a stated RPS); a runbook has been dry-run at least once.

---

# Security-Model.md — Draft Outline

The main document names this file but doesn't specify its contents. Suggested sections:

**1. Identity & Access**
Authentication flow (OIDC/OAuth2 grant types used), role/claims model, service-to-service auth (client credentials or mTLS).

**2. Token Lifecycle**
Access token TTL, refresh token rotation and revocation strategy, signing key rotation policy (and where keys live — not in appsettings.json).

**3. Secrets Management**
What goes in K8s Secrets vs. an external store (Vault / external-secrets), how CI/CD accesses secrets without printing them to logs, secret rotation cadence.

**4. Network Security**
NetworkPolicies restricting which services can talk to which, ingress TLS termination, whether internal traffic is plaintext or mTLS.

**5. Data Protection**
Encryption at rest for Postgres, PII field-level handling, what "payment data" is simulated vs. real (important to state explicitly since this is a simulation, not a real PCI-DSS-scoped system).

**6. Application Security Controls**
Input validation strategy, rate limiting, idempotency enforcement on financial endpoints, dependency and secrets scanning in CI.

**7. Threat Modeling**
Per-service STRIDE notes, with the Wallet and Payment services prioritized first given they're the highest-value targets.

**8. Security Testing**
OWASP ZAP baseline scans, how findings are triaged and tracked, cadence (e.g., before each phase's "production-ready" sign-off).

**9. Incident Response (links to `engineering:incident-response` style workflow)**
Who "responds" in a solo/simulated context, how a security incident is logged and post-mortemed.

---

*Suggested next step: replace the "PHASES 4–17" section in the main instruction file with the phase breakdown above, and add `Security-Model.md` to the Phase 1 documentation list with the outline above as its starting skeleton.*
