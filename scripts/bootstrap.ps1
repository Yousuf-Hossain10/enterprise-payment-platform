# Stands up the local platform against Docker Desktop's built-in Kubernetes
# cluster (context "docker-desktop") rather than a standalone `kind` cluster
# -- see the "Local Cluster" deviation note in docs/Deployment-Strategy.md
# for why. Namespaces + Postgres/RabbitMQ/Redis (Days 7-8) + NGINX Ingress
# and the Prometheus/Grafana/Loki observability stack (Day 9) are all
# provisioned here; teardown.ps1 (Day 10) is this script's counterpart.
$ErrorActionPreference = "Stop"

$Context = if ($env:KUBE_CONTEXT) { $env:KUBE_CONTEXT } else { "docker-desktop" }
$RepoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "==> Verifying Docker Desktop Kubernetes is reachable (context: $Context)"
$contexts = kubectl config get-contexts -o name
if ($contexts -notcontains $Context) {
    Write-Error "Context '$Context' not found. Enable Kubernetes in Docker Desktop settings, or set `$env:KUBE_CONTEXT to the right context name."
    exit 1
}
kubectl config use-context $Context | Out-Null
kubectl get nodes | Out-Null

Write-Host "==> Creating namespaces"
foreach ($ns in @("platform", "monitoring", "ingress-nginx")) {
    kubectl create namespace $ns --dry-run=client -o yaml | kubectl apply -f -
}

Write-Host "==> Adding Helm repositories"
helm repo add bitnami https://charts.bitnami.com/bitnami | Out-Null
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx | Out-Null
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts | Out-Null
helm repo add grafana https://grafana.github.io/helm-charts | Out-Null
helm repo update | Out-Null

Write-Host "==> Installing infra: Postgres, RabbitMQ, Redis"
helm upgrade --install postgres bitnami/postgresql --version 18.8.8 `
  -n platform -f "$RepoRoot\infra\postgres-values.yaml" --wait --timeout 5m
helm upgrade --install rabbitmq bitnami/rabbitmq --version 16.0.14 `
  -n platform -f "$RepoRoot\infra\rabbitmq-values.yaml" --wait --timeout 5m
helm upgrade --install redis bitnami/redis --version 28.0.2 `
  -n platform -f "$RepoRoot\infra\redis-values.yaml" --wait --timeout 5m

Write-Host "==> Installing NGINX Ingress"
helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx --version 4.15.1 `
  -n ingress-nginx --wait --timeout 5m

Write-Host "==> Installing observability stack: kube-prometheus-stack, Loki"
helm upgrade --install kube-prometheus-stack prometheus-community/kube-prometheus-stack --version 88.3.0 `
  -n monitoring --wait --timeout 8m
helm upgrade --install loki grafana/loki-stack --version 2.10.3 `
  -n monitoring -f "$RepoRoot\infra\loki-values.yaml" --wait --timeout 5m

Write-Host "==> Bootstrap complete (namespaces, Postgres/RabbitMQ/Redis, Ingress, and observability stack as of Phase 3, Day 9)"
