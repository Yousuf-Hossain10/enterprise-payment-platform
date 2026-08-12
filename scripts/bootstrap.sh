#!/usr/bin/env bash
# Stands up the local platform against Docker Desktop's built-in Kubernetes
# cluster (context "docker-desktop") rather than a standalone `kind` cluster
# — see the "Local Cluster" deviation note in docs/Deployment-Strategy.md
# for why. Namespaces only today (Day 7); Postgres/RabbitMQ/Redis (Day 8),
# Ingress + observability stack (Day 9) are appended here as those days land.
set -euo pipefail

CONTEXT="${KUBE_CONTEXT:-docker-desktop}"

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

echo "==> Bootstrap complete (namespaces only as of Phase 3, Day 7)"
