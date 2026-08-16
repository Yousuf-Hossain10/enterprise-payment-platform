# Deployment Strategy

## Local Cluster: Docker Desktop Kubernetes, Not Kind

The tutorial specifies a standalone `kind` cluster for local development. This project deviates: local development runs against **Docker Desktop's built-in Kubernetes** (`kubectl` context `docker-desktop`) instead.

**Why:** the `kind` CLI's binary distribution is served from `github.com`, which is unreachable from the sandboxed environment this project is built in — `kind` couldn't be installed there, while Docker Desktop's Kubernetes toggle (which itself runs `kindest/node` images under the hood) was already available and required no additional download. Functionally the two are close to interchangeable for this project's purposes — both are a real, local Kubernetes control plane — so this is a tooling substitution, not a design change: namespace layout, manifests, Helm charts, and NetworkPolicies are all written against plain Kubernetes APIs and are portable to a real `kind` cluster (or any other cluster) without modification if the tooling constraint is later lifted.

`scripts/bootstrap.sh`/`.ps1` verify the `docker-desktop` context is reachable (overridable via `$KUBE_CONTEXT`/`KUBE_CONTEXT` env var) rather than creating a cluster via `kind create cluster`. There is accordingly no `kind-config.yaml` in this repo.

**Node count and resources:** Docker Desktop's Kubernetes defaults to a multi-node topology (1 control-plane + 5 workers) on this version, which is unnecessary overhead for a solo local capstone — none of this project's Definition of Done checks (NetworkPolicy enforcement, HPA scaling, the Phase 17 chaos test) require multiple physical nodes to validate. The cluster is configured for **1 node**, with Docker Desktop given **7GiB memory / 6 CPUs**, via Docker Desktop's Settings → Resources and Settings → Kubernetes (not something this repo's scripts control — it's host-machine configuration, same category as having Docker installed at all). At the default multi-node topology, this host's available RAM was insufficient to keep the full Postgres/RabbitMQ/Redis/Ingress/observability stack stable (sustained `MemoryPressure`, multi-minute API latency); single-node resolved it.

## Local Infra Images: `bitnamilegacy`, Not `bitnami`, for RabbitMQ

As of this writing, `docker.io/bitnami/rabbitmq` has **zero published tags** — Broadcom's August 2025 restructuring of the Bitnami catalog moved free-tier images off the `bitnami/*` namespace, in RabbitMQ's case leaving nothing behind at all (Postgres and Redis's `bitnami/*` repos still serve a `latest` tag, so their charts install unmodified). The frozen historical images now live under `bitnamilegacy/*`.

`infra/rabbitmq-values.yaml` overrides `image.repository` to `bitnamilegacy/rabbitmq` at the exact tag the chart expects (`4.1.3-debian-12-r1`), plus `global.security.allowInsecureImages: true`, which the chart requires to deploy an image outside its known-good list (documented upstream workaround, [bitnami/charts#30850](https://github.com/bitnami/charts/issues/30850)). This is a pinned, verified-working workaround for a registry-availability problem, not an endorsement of the `bitnamilegacy` namespace as a long-term source — if this chart's default image is restored to a working state upstream, this override should be removed.

## Grafana + Loki: Only One Default Datasource

`grafana/loki-stack`'s chart defaults `loki.isDefault: true` — it provisions a Loki datasource ConfigMap for Grafana's sidecar-based datasource discovery, and marks it default. Since Grafana itself comes from `kube-prometheus-stack` (installed into the same `monitoring` namespace) and *that* chart already marks its own Prometheus datasource as default, Grafana's sidecar picks up both ConfigMaps and refuses to start: `"Only one datasource per organization can be marked as default"`.

`infra/loki-values.yaml` overrides `loki.isDefault: false` — Prometheus remains the default datasource, Loki is registered as a secondary one. Install order matters here in principle (`kube-prometheus-stack` before `loki`, so Grafana exists for the sidecar to attach to), and `scripts/bootstrap.sh`/`.ps1` install them in that order.

## Environments

Three logical environments, all runnable against the same local Docker Desktop Kubernetes cluster during development (Phase 3) and later distinguished purely by Helm values (Phase 13) rather than different infrastructure code:

| Environment | Purpose | Replicas | Resource limits | Namespace |
|---|---|---|---|---|
| **dev** | Local iteration, the default for this whole project since there's no real cloud target | 1 per service | Relaxed | `platform` |
| **staging** | A dress rehearsal config — same manifests, prod-like replica counts and limits, still deployable to the local cluster or a throwaway cloud cluster if one is ever stood up | 2 per service | Prod-like | `platform-staging` |
| **prod** | The "as if this were real" target — stricter limits, `podAntiAffinity`, manual approval gate in CI (Phase 14) | 3 per service | Strict | `platform-prod` |

Since this is a solo capstone with one physical cluster, staging and prod are primarily exercised as **configuration variants** — proving the Helm values-per-environment story works (Phase 13's Definition of Done) — rather than genuinely separate infrastructure. This is stated explicitly so the distinction isn't mistaken for a claim of real multi-environment infrastructure.

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
