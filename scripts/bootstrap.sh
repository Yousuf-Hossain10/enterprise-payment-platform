#!/usr/bin/env bash
# Stands up the local platform against Docker Desktop's built-in Kubernetes
# cluster (context "docker-desktop") rather than a standalone `kind` cluster
# — see the "Local Cluster" deviation note in docs/Deployment-Strategy.md
# for why. Namespaces + Postgres/RabbitMQ/Redis today (Days 7-8); Ingress +
# observability stack (Day 9) are appended here as that day lands.
set -euo pipefail

CONTEXT="${KUBE_CONTEXT:-docker-desktop}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "==> Verifying Docker Desktop Kubernetes is reachable (context: $CONTEXT)"
if ! kubectl config get-contexts "$CONTEXT" >/dev/null 2>&1; then
  echo "Context '$CONTEXT' not found. Enable Kubernetes in Docker Desktop settings, or set KUBE_CONTEXT to the right context name." >&2
  exit 1
fi
kubectl config use-context "$CONTEXT" >/dev/null
kubectl get nodes >/dev/null

echo "==> Creating namespaces"
for ns in platform monitoring ingress-nginx; do
  kubectl create namespace "$ns" --dry-run=client -o yaml | kubectl apply -f -
done

echo "==> Adding Helm repositories"
helm repo add bitnami https://charts.bitnami.com/bitnami >/dev/null
helm repo update >/dev/null

echo "==> Installing infra: Postgres, RabbitMQ, Redis"
helm upgrade --install postgres bitnami/postgresql --version 18.8.8 \
  -n platform -f "$REPO_ROOT/infra/postgres-values.yaml" --wait --timeout 5m
helm upgrade --install rabbitmq bitnami/rabbitmq --version 16.0.14 \
  -n platform -f "$REPO_ROOT/infra/rabbitmq-values.yaml" --wait --timeout 5m
helm upgrade --install redis bitnami/redis --version 28.0.2 \
  -n platform -f "$REPO_ROOT/infra/redis-values.yaml" --wait --timeout 5m

echo "==> Bootstrap complete (namespaces + Postgres/RabbitMQ/Redis as of Phase 3, Day 8)"
