#!/usr/bin/env bash
# Stands up the local platform against Docker Desktop's built-in Kubernetes
# cluster (context "docker-desktop") rather than a standalone `kind` cluster
# — see the "Local Cluster" deviation note in docs/Deployment-Strategy.md
# for why. Namespaces + Postgres/RabbitMQ/Redis (Days 7-8) + NGINX Ingress
# and the Prometheus/Grafana/Loki observability stack (Day 9) are all
# provisioned here; teardown.sh (Day 10) is this script's counterpart.
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
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx >/dev/null
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts >/dev/null
helm repo add grafana https://grafana.github.io/helm-charts >/dev/null
helm repo update >/dev/null

echo "==> Installing infra: Postgres, RabbitMQ, Redis"
helm upgrade --install postgres bitnami/postgresql --version 18.8.8 \
  -n platform -f "$REPO_ROOT/infra/postgres-values.yaml" --wait --timeout 5m
helm upgrade --install rabbitmq bitnami/rabbitmq --version 16.0.14 \
  -n platform -f "$REPO_ROOT/infra/rabbitmq-values.yaml" --wait --timeout 5m
helm upgrade --install redis bitnami/redis --version 28.0.2 \
  -n platform -f "$REPO_ROOT/infra/redis-values.yaml" --wait --timeout 5m

echo "==> Installing NGINX Ingress"
helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx --version 4.15.1 \
  -n ingress-nginx --wait --timeout 5m

echo "==> Installing observability stack: kube-prometheus-stack, Loki"
helm upgrade --install kube-prometheus-stack prometheus-community/kube-prometheus-stack --version 88.3.0 \
  -n monitoring --wait --timeout 8m
helm upgrade --install loki grafana/loki-stack --version 2.10.3 \
  -n monitoring -f "$REPO_ROOT/infra/loki-values.yaml" --wait --timeout 5m

echo "==> Creating per-service databases (database-per-service, docs/Microservice-Responsibilities.md)"
for db in identity wallet payment; do
  kubectl exec -n platform postgres-postgresql-0 -- env PGPASSWORD=local-dev-postgres-admin psql -U postgres -tc \
    "SELECT 1 FROM pg_database WHERE datname = '$db'" | grep -q 1 || \
    kubectl exec -n platform postgres-postgresql-0 -- env PGPASSWORD=local-dev-postgres-admin psql -U postgres \
      -c "CREATE DATABASE $db OWNER payment_platform;"
done

echo "==> Bootstrap complete (namespaces, Postgres/RabbitMQ/Redis, Ingress, observability stack, and per-service databases)"
