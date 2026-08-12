# Sprint Plan — Day-by-Day Build Schedule

*Companion to the tutorial, ADR log, learning journal, and concept study guide. Paced for ~12 hours/week (4 sessions × 3 hours), covering all 19 phases (17 core + 2 extensions).*

At this pace the full build runs **~24 weeks (~6 months)**. Treat that as a compass, not a deadline — some phases (Wallet, Payment, Angular, Observability) genuinely deserve more time if a concept isn't clicking yet. Your stated goal is maximum learning outcome, not shipping speed, so a session that runs long because you're actually understanding optimistic concurrency for the first time is a session well spent. Skipping the ADR/journal work at the end of a phase to "stay on schedule" is the one thing not to do — that's where the recruiter-facing value of this repo actually comes from.

**Sessions are generic** ("Day 1", "Day 2"...) rather than tied to real weekdays — map them onto whatever four days a week actually work for you. Week numbers are shown for reference only.

---

## Commit Message Conventions

Use [Conventional Commits](https://www.conventionalcommits.org/): `type(scope): imperative summary`, written the way you'd want to see it in a PR you're reviewing, not the way you'd narrate it to yourself.

**Types you'll use most:** `feat` (new capability), `fix` (bug fix), `test` (tests only), `docs` (documentation/ADRs/diagrams), `ci` (pipeline changes), `chore` (scaffolding, deps, non-functional), `refactor` (no behavior change), `perf` (performance work).

**Good:** `feat(wallet): add optimistic concurrency check on debit`
**Bad:** `fix stuff`, `wip`, `updates`

For anything non-trivial (a saga, a concurrency fix, a security decision), add a body explaining *why*, not just *what* — the diff already shows what:

```
fix(payment): fail saga closed when wallet debit times out

Previously a Wallet timeout was treated as a transient error and
retried indefinitely, which could leave a payment stuck in
Authorized. Now a timeout after the retry budget is exhausted marks
the payment Failed immediately. See ADR-0002 for the broader
fail-open/fail-closed reasoning.
```

**Cadence:** commit at the end of every session, even mid-phase — small atomic commits beat one giant end-of-phase dump, and they're what actually make your commit history readable to a recruiter skimming it. Each day below ends with one commit message; if your session naturally produces two logical checkpoints, split it into two commits.

## Running Sessions With Claude Code

Each session, point Claude Code at this folder and give it three things: (1) the specific Day N tasks below, (2) the relevant phase's Definition of Done from `Enterprise_Payment_Platform_Tutorial.md`, and (3) an explicit instruction to stop and let you review the diff before committing — this mirrors the plan's own "Agent Execution Rules" (one phase at a time, wait for review). Write the ADR and journal entries yourself, in your own words, even if Claude Code drafts the code — that reasoning is the part that's actually yours to own, and it's what you're being asked to explain in an interview, not the code.

---

## Phase 1 — Architecture & Planning *(Week 1, Days 1–4)*

**Sprint goal:** no code exists yet — only a design a senior engineer would sign off on.

| Day | Focus | Commit |
|---|---|---|
| 1 | Write `Architecture.md`, `Technology-Decisions.md`; start the ADR habit | `docs: add initial architecture overview and ADR log skeleton` |
| 2 | Write `Folder-Structure.md`, `Coding-Standards.md`, `Microservice-Responsibilities.md` | `docs: define folder structure, coding standards, and service boundaries` |
| 3 | Write `API-Guidelines.md`, `Deployment-Strategy.md`, `Security-Model.md` outline, `Logging-Strategy.md`, `Observability-Strategy.md` | `docs: add api guidelines, deployment strategy, and security/observability models` |
| 4 | Draw Mermaid diagrams (context, container, component, deployment, 7 sequence diagrams); write `Development-Roadmap.md` | `docs: add system diagrams and development roadmap` |

## Phase 2 — Repository Initialization *(Week 2, Days 5–6)*

**Sprint goal:** a monorepo skeleton that builds, with nothing implemented yet.

| Day | Focus | Commit |
|---|---|---|
| 5 | `git init`, `.gitignore`, solution scaffold, Clean Architecture layers per service | `chore: scaffold monorepo solution and clean architecture project layout` |
| 6 | Scaffold Angular app, verify `dotnet build` + `ng build` both succeed, root `README.md` | `chore: scaffold angular frontend and verify full solution builds` |

## Phase 3 — Local Infrastructure *(Weeks 2–3, Days 7–10)*

**Sprint goal:** one script stands up the entire local platform.

| Day | Focus | Commit |
|---|---|---|
| 7 | `kind-config.yaml`, `bootstrap.sh` skeleton, cluster + namespace creation | `feat(infra): add kind cluster config and namespace bootstrap` |
| 8 | Add Postgres/RabbitMQ/Redis Helm installs to bootstrap script | `feat(infra): provision postgres, rabbitmq, and redis via bootstrap script` |
| 9 | Add NGINX Ingress, Prometheus/Grafana/Loki installs | `feat(infra): add ingress and observability stack to bootstrap script` |
| 10 | Add `teardown.sh`, test full bootstrap/teardown cycle, document env vars in `Deployment-Strategy.md` | `feat(infra): add teardown script and verify full bootstrap cycle` |

## Phase 4 — Shared Backend Foundation Libraries *(Weeks 3–4, Days 11–16)*

**Sprint goal:** every service will import these instead of reinventing them.

| Day | Focus | Commit |
|---|---|---|
| 11 | `BuildingBlocks.Common` — `Result<T>`, Problem Details middleware | `feat(common): add result type and global exception middleware` |
| 12 | Correlation-ID middleware, FluentValidation base validators, typed config helpers | `feat(common): add correlation id propagation and typed configuration` |
| 13 | `BuildingBlocks.Messaging` — outbox message model + background dispatcher | `feat(messaging): implement outbox pattern with background dispatcher` |
| 14 | Idempotent-consumer helper (`ProcessedEvents` pattern), outbox unit tests | `feat(messaging): add idempotent consumer helper and outbox tests` |
| 15 | `BuildingBlocks.Observability` — OpenTelemetry tracing/metrics, health-check base | `feat(observability): wire opentelemetry tracing, metrics, and health checks` |
| 16 | `BuildingBlocks.Security` — JWT middleware, permission attribute; READMEs; throwaway "ping" service proving composition | `feat(security): add jwt middleware and permission-based authorization` |

## Phase 5 — Identity Service *(Weeks 5–6, Days 17–22)*

**Sprint goal:** every other service can trust a token this service issued.

| Day | Focus | Commit |
|---|---|---|
| 17 | Domain model (`User`, `RefreshToken`), EF Core migrations | `feat(identity): add user and refresh token domain model with migrations` |
| 18 | Register/login endpoints, Argon2id password hashing | `feat(identity): implement registration and login with argon2id hashing` |
| 19 | Access/refresh token issuance, JWT claims design | `feat(identity): implement jwt access and refresh token issuance` |
| 20 | Refresh token rotation + revocation | `feat(identity): add refresh token rotation and revocation` |
| 21 | Testcontainers integration tests, OpenAPI spec | `test(identity): add testcontainers integration tests and openapi spec` |
| 22 | Login rate limiting; `Security-Model.md` threat notes; ADR-0004/0005; journal entry | `feat(identity): add login rate limiting` + `docs: security notes and identity adrs` |

## Phase 6 — Wallet Service *(Weeks 6–8, Days 23–30)*

**Sprint goal:** money can never be created, destroyed, or double-spent by a bug. This is the phase to slow down for.

| Day | Focus | Commit |
|---|---|---|
| 23 | Domain model — `Account`, `LedgerEntry`, RowVersion concurrency token | `feat(wallet): add account and ledger entry domain model` |
| 24 | `DebitAsync` with idempotency-key check and computed balance | `feat(wallet): implement idempotent debit with balance computation` |
| 25 | `CreditAsync`, optimistic concurrency exception handling | `feat(wallet): implement credit operation and concurrency conflict handling` |
| 26 | Outbox integration — `WalletDebited`/`WalletCredited` events | `feat(wallet): publish ledger events via outbox pattern` |
| 27 | Write the parallel-debit concurrency test (20+ concurrent requests) | `test(wallet): add parallel debit concurrency test` |
| 28 | Debug/harden edge cases the concurrency test surfaces | `fix(wallet): handle edge cases in concurrent debit retries` |
| 29 | Reconciliation job/endpoint (ledger sum vs. balance) | `feat(wallet): add ledger reconciliation report` |
| 30 | ADR-0006/0007; journal entry on optimistic concurrency (flagship post material); DoD review | `docs: add wallet adrs and concurrency journal entry` |

## Phase 7 — Payment Service *(Weeks 8–10, Days 31–38)*

**Sprint goal:** a failure mid-flow never leaves a payment stuck.

| Day | Focus | Commit |
|---|---|---|
| 31 | Payment state machine with explicit legal-transition guards | `feat(payment): implement payment state machine with transition guards` |
| 32 | Wallet HTTP client + Polly retry/circuit-breaker policies | `feat(payment): add resilient wallet client with retry and circuit breaker` |
| 33 | `CapturePaymentAsync` orchestration (the saga) | `feat(payment): implement capture orchestration saga` |
| 34 | `Idempotency-Key` header enforcement on `POST /payments` | `feat(payment): enforce idempotency key on payment creation` |
| 35 | Outbox integration — `PaymentCaptured`/`PaymentFailed` events | `feat(payment): publish payment lifecycle events via outbox` |
| 36 | Fault-injection test — kill Wallet mid-saga | `test(payment): add fault injection test for wallet outage mid-saga` |
| 37 | Contract tests against Wallet/Notification | `test(payment): add contract tests for wallet and notification dependencies` |
| 38 | Finalize ADR-0002 with real implementation notes; journal entry; DoD review | `docs: finalize payment saga adr and journal entry` |

## Phase 8 — Notification Service *(Weeks 10–11, Days 39–42)*

**Sprint goal:** every event gets delivered exactly once, from the caller's perspective.

| Day | Focus | Commit |
|---|---|---|
| 39 | RabbitMQ consumer scaffold, subscribe to payment events | `feat(notification): add rabbitmq consumer for payment events` |
| 40 | Idempotent handling via `ProcessedEvents`, mock email/SMS templates | `feat(notification): add idempotent event handling and message templates` |
| 41 | Dead-letter queue + depth alert | `feat(notification): configure dead-letter queue and depth alerting` |
| 42 | Test: duplicate event delivery → one notification; journal entry | `test(notification): verify duplicate events produce single notification` |

## Phase 9 — Audit Service *(Weeks 11–12, Days 43–46)*

**Sprint goal:** an immutable record of everything, independent of every other service's DB.

| Day | Focus | Commit |
|---|---|---|
| 43 | Append-only `AuditEntries` schema, DB-level write restrictions | `feat(audit): add append-only audit schema with restricted write access` |
| 44 | Consume events from all services (reuse idempotent pattern) | `feat(audit): consume domain events from all services` |
| 45 | Paginated/filterable query API | `feat(audit): add paginated audit query api` |
| 46 | Hash-chaining for tamper evidence; seed 50k+ rows, check query perf; journal entry | `feat(audit): add hash chain tamper evidence and verify query performance` |

## Phase 10 — Angular Frontend *(Weeks 12–14, Days 47–54)*

**Sprint goal:** one coherent client, talking only to the Gateway.

| Day | Focus | Commit |
|---|---|---|
| 47 | Gateway/BFF scaffold (YARP), route config for all services | `feat(gateway): scaffold yarp reverse proxy with service routes` |
| 48 | Angular app shell, feature module structure | `chore(frontend): scaffold feature module structure` |
| 49 | Login flow, auth interceptor, silent token refresh | `feat(frontend): implement login flow with silent token refresh` |
| 50 | Wallet balance view, payment creation form | `feat(frontend): add wallet balance view and payment form` |
| 51 | Signal-based state management, error handling/toasts | `feat(frontend): wire signal-based state management and error handling` |
| 52 | Playwright e2e happy path (login → balance → payment → updated balance) | `test(frontend): add end-to-end happy path test suite` |
| 53 | Accessibility pass (axe-core), fix contrast/label issues | `fix(frontend): resolve accessibility violations on core pages` |
| 54 | ADR-0009/0010 (state mgmt, gateway choice); journal entry; DoD review | `docs: add frontend architecture adrs and journal entry` |

## Phase 11 — Docker Packaging *(Week 14, Days 55–56)*

**Sprint goal:** small, non-root, scanned images for every deployable.

| Day | Focus | Commit |
|---|---|---|
| 55 | Multi-stage Dockerfiles for all 5 services + gateway (non-root, alpine) | `feat(docker): add multi-stage non-root dockerfiles for all services` |
| 56 | Angular Dockerfile (nginx), Trivy scan, image size budget check | `feat(docker): add frontend image and trivy vulnerability scanning` |

## Phase 12 — Kubernetes Manifests *(Weeks 15–16, Days 57–62)*

**Sprint goal:** declarative, least-privilege deployment definitions.

| Day | Focus | Commit |
|---|---|---|
| 57 | Deployment + Service manifests — Identity, Wallet | `feat(k8s): add deployment and service manifests for identity and wallet` |
| 58 | Deployment + Service manifests — Payment, Notification, Audit, Gateway | `feat(k8s): add deployment and service manifests for remaining services` |
| 59 | ConfigMaps, sealed Secrets, Ingress | `feat(k8s): add configmaps, sealed secrets, and ingress routing` |
| 60 | HPA + PodDisruptionBudget per service | `feat(k8s): add horizontal pod autoscaling and disruption budgets` |
| 61 | NetworkPolicies (default-deny + explicit allows) | `feat(k8s): enforce default-deny network policies between services` |
| 62 | `kubeconform` validation, apply to Kind, verify NetworkPolicy blocks disallowed calls; journal entry | `test(k8s): validate manifests and verify network policy enforcement` |

## Phase 13 — Helm Charts *(Weeks 16–17, Days 63–66)*

**Sprint goal:** environment-parameterized packaging of Phase 12's manifests.

| Day | Focus | Commit |
|---|---|---|
| 63 | Umbrella chart scaffold, subcharts per service | `feat(helm): scaffold umbrella chart with per-service subcharts` |
| 64 | Parameterize `values.yaml` per dev/staging/prod | `feat(helm): add environment-specific values files` |
| 65 | Golden-file tests (`helm template` output committed) | `test(helm): add golden file tests for chart rendering` |
| 66 | `helm lint` + `ct lint` clean; ADR-0013; journal entry | `docs: finalize helm vs kustomize adr` |

## Phase 14 — GitHub Actions CI/CD *(Weeks 17–18, Days 67–72)*

**Sprint goal:** the PR checklist enforces itself.

| Day | Focus | Commit |
|---|---|---|
| 67 | Build/test workflow (`dotnet build --warnaserror`, `dotnet test`) | `ci: add dotnet build and test workflow` |
| 68 | Frontend workflow (`npm ci`, build, Playwright) | `ci: add frontend build and e2e test workflow` |
| 69 | Docker build + Trivy scan job | `ci: add docker build and vulnerability scan job` |
| 70 | Helm lint/template + `kubeconform` validation job | `ci: add helm and kubernetes manifest validation job` |
| 71 | Self-hosted runner setup, branch protection rules | `ci: configure self-hosted runner and branch protection` |
| 72 | Prod deploy job with manual approval gate; rehearse rollback; ADR-0014; journal entry | `ci: add manual-approval production deploy job` |

## Phase 15 — Observability Stack *(Weeks 19–20, Days 73–78)*

**Sprint goal:** an incident becomes explainable in minutes.

| Day | Focus | Commit |
|---|---|---|
| 73 | Grafana RED dashboards per service | `feat(observability): add per-service red method dashboards` |
| 74 | Platform-wide dashboard (aggregate RED + RabbitMQ/Postgres saturation) | `feat(observability): add platform-wide aggregate dashboard` |
| 75 | Loki correlation-ID query verification across all services | `test(observability): verify cross-service log correlation via loki` |
| 76 | Deploy Tempo, wire OTLP exporter across all services | `feat(observability): deploy tempo and wire distributed tracing` |
| 77 | Verify a single trace spans the full payment flow end-to-end | `test(observability): verify end-to-end trace across payment flow` |
| 78 | Alertmanager rules (error rate, latency, DLQ depth, pool saturation), trigger a test alert; ADR-0015; journal entry | `feat(observability): add alerting rules for slo violations` |

## Phase 16 — Security Hardening *(Weeks 20–21, Days 79–82)*

**Sprint goal:** close the gap between "runs" and "production-inspired."

| Day | Focus | Commit |
|---|---|---|
| 79 | STRIDE threat model for Wallet, Payment, Identity | `docs: add stride threat models for critical services` |
| 80 | Dependabot/Snyk + gitleaks CI integration | `ci: add dependency and secret scanning` |
| 81 | Rate limiting at the Gateway (login + payments) | `feat(gateway): add rate limiting on sensitive endpoints` |
| 82 | OWASP ZAP baseline scan, triage findings; ADR-0016; journal entry | `docs: document zap scan results and mtls deferral decision` |

## Phase 17 — Comprehensive Testing & Documentation *(Weeks 21–22, Days 83–88)*

**Sprint goal:** prove it holds up under load and failure, and leave it usable by someone else.

| Day | Focus | Commit |
|---|---|---|
| 83 | Document test pyramid ratios, audit current coverage gaps | `docs: define test pyramid strategy and coverage targets` |
| 84 | k6 load test script against the payment endpoint | `test(load): add k6 load test for payment endpoint` |
| 85 | Run load test, tune until the SLA is met (p95 target) | `perf(payment): tune capture path to meet p95 latency sla` |
| 86 | Chaos test — kill a Wallet pod under load | `test(chaos): verify graceful recovery on wallet pod failure under load` |
| 87 | Refresh all Phase 1 docs to match the final implementation | `docs: refresh architecture docs to match final implementation` |
| 88 | Write on-call, incident-response, backup/restore runbooks; dry-run each; ADR-0017; journal entry | `docs: add operational runbooks and dry-run results` |

## Phase 18 — Fraud/Risk Service *(Week 23, Days 89–92, extension)*

**Sprint goal:** a real gate-check step in the saga, and an honest answer to "what happens when it's down."

| Day | Focus | Commit |
|---|---|---|
| 89 | Domain model (`RiskAssessment`, `IRiskRule`); Velocity, LargeAmount, FirstTimePayee rules | `feat(risk): add rule-based risk assessment engine` |
| 90 | Wire risk check into the Payment saga (`RiskAssessed` state) | `feat(payment): integrate risk assessment into capture saga` |
| 91 | Fail-open/fail-closed handling + manual review queue endpoint | `feat(risk): add configurable failure mode and manual review queue` |
| 92 | Unit tests per rule; integration test for all 3 decision outcomes; ADR-0018; journal entry | `test(risk): cover all risk decision outcomes with integration tests` |

## Phase 19 — Reporting/Analytics Service *(Week 24, Days 93–96, extension)*

**Sprint goal:** make CQRS concrete, and prove the read side is truly disposable.

| Day | Focus | Commit |
|---|---|---|
| 93 | Read model schema (`DailyTransactionSummary`, `AccountBalanceSnapshot`) | `feat(reporting): add read-optimized reporting schema` |
| 94 | Event consumer with idempotent upserts | `feat(reporting): consume domain events into materialized read models` |
| 95 | Query API with staleness indicators | `feat(reporting): add reporting query api with staleness metadata` |
| 96 | Rebuild-from-Audit-log script; verify it matches a pre-drop snapshot; ADR-0019; final journal entry + capstone retrospective | `feat(reporting): add rebuild-from-event-log capability` |

---

## After Day 96

Write a closing retrospective in the Learning Journal: which ADR you'd revisit with hindsight, which phase taught you the most, and what you'd do differently starting over. That retrospective — not the code — is often the single most compelling thing to walk an interviewer through, because it's the part no tutorial could have handed you.
