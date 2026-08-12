# Deployment Strategy

## Environments

Three logical environments, all runnable against the same local Kind cluster during development (Phase 3) and later distinguished purely by Helm values (Phase 13) rather than different infrastructure code:

| Environment | Purpose | Replicas | Resource limits | Namespace |
|---|---|---|---|---|
| **dev** | Local iteration, the default for this whole project since there's no real cloud target | 1 per service | Relaxed | `platform` (Kind) |
| **staging** | A dress rehearsal config — same manifests, prod-like replica counts and limits, still deployable to the local Kind cluster or a throwaway cloud cluster if one is ever stood up | 2 per service | Prod-like | `platform-staging` |
| **prod** | The "as if this were real" target — stricter limits, `podAntiAffinity`, manual approval gate in CI (Phase 14) | 3 per service | Strict | `platform-prod` |

Since this is a solo capstone with one physical cluster (Kind), staging and prod are primarily exercised as **configuration variants** — proving the Helm values-per-environment story works (Phase 13's Definition of Done) — rather than genuinely separate infrastructure. This is stated explicitly so the distinction isn't mistaken for a claim of real multi-environment infrastructure.

## How Helm Values Differ Per Environment

Introduced in Phase 13; the shape is fixed here so later phases build toward it:

- `values.yaml` — shared defaults (image repository, common labels, probe paths).
- `values-dev.yaml` — `replicaCount: 1`, minimal CPU/memory requests, `NodePort`/port-forward friendly service types, verbose logging (`Debug` level).
- `values-staging.yaml` — `replicaCount: 2`, resource requests/limits matching prod's shape but at lower absolute values, `Information` logging.
- `values-prod.yaml` — `replicaCount: 3`, `podAntiAffinity` so replicas spread across nodes, stricter resource limits, `PodDisruptionBudget` minimums enforced, `Information` logging with sampling on verbose traces.

Only image tag, replica count, resource limits, and log verbosity vary per environment — the manifest *shape* (which objects exist) does not, so a bug caught in dev is testing the same topology prod runs.

## Rollout Strategy

- **Rolling update** is the default strategy for every service's Deployment (`RollingUpdate` with `maxUnavailable: 0`, `maxSurge: 1`), enforced by the `PodDisruptionBudget` added in Phase 12 (Day 60). This is the right default for a set of stateless services behind a Service/Ingress with no session affinity requirement.
- **Blue/green is not used** for this platform — the added infrastructure cost (running two full environments simultaneously) isn't justified at this scale, and rolling updates combined with health-checked readiness probes give equivalent safety for a single-cluster deployment. This is a deliberate simplification, not an oversight; it's noted here so it's a documented decision rather than a silent gap when Phase 17 refreshes this doc.
- Database migrations (EF Core) run as a pre-deploy step (a Kubernetes `Job` or CI pipeline step, finalized in Phase 14), never inside application startup — a pod that crash-loops on startup shouldn't be able to leave a migration half-applied.
- Every rollout depends on the readiness probes wired in Phase 4 (`BuildingBlocks.Observability`'s health-check base) — a pod isn't considered "up" for traffic routing purposes until its readiness endpoint reports healthy, including a real check against its database connection.

## Required Environment Variables & Secrets

*(Expanded in full once `scripts/bootstrap.sh`/`.ps1` exist — Phase 3, Day 10 — this section is a placeholder structure, not yet populated, since nothing is provisioned yet as of Phase 1.)*

Anticipated categories, to be filled in with concrete names as each is introduced:

- **Database** — per-service Postgres connection string (host, port, credentials), sourced from a K8s Secret in dev, from external-secrets/Vault in a hypothetical real prod (see `Security-Model.md`).
- **Messaging** — RabbitMQ connection string/credentials.
- **Identity/JWT** — signing key (or reference to where it's stored — never in `appsettings.json`), token TTLs.
- **Observability** — OTLP exporter endpoint (Tempo/Prometheus collector address).
- **Gateway** — per-service base URLs for YARP routing (or service-discovery equivalent within the cluster).

## Rollback

- Helm's built-in revision history (`helm rollback <release> <revision>`) is the rollback mechanism — no bespoke tooling. This is rehearsed for real as part of Phase 14 (Day 72: "rehearse rollback") and documented as a runbook in Phase 17 (Day 88).
