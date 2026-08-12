# Stands up the local platform against Docker Desktop's built-in Kubernetes
# cluster (context "docker-desktop") rather than a standalone `kind` cluster
# -- see the "Local Cluster" deviation note in docs/Deployment-Strategy.md
# for why. Namespaces only today (Day 7); Postgres/RabbitMQ/Redis (Day 8),
# Ingress + observability stack (Day 9) are appended here as those days land.
$ErrorActionPreference = "Stop"

$Context = if ($env:KUBE_CONTEXT) { $env:KUBE_CONTEXT } else { "docker-desktop" }

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

Write-Host "==> Bootstrap complete (namespaces only as of Phase 3, Day 7)"
