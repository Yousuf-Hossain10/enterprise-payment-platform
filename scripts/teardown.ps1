# Counterpart to bootstrap.ps1. The tutorial's teardown deletes the kind
# cluster itself (`kind delete cluster`); since this project runs against
# Docker Desktop's built-in Kubernetes instead (see the "Local Cluster"
# note in docs/Deployment-Strategy.md), there is no cluster to delete --
# tearing down means removing everything bootstrap.ps1 provisioned inside
# it: the Helm releases, then the namespaces (which also cleans up their
# PVCs, since the default StorageClass's reclaim policy is Delete).
$ErrorActionPreference = "Stop"

$Context = if ($env:KUBE_CONTEXT) { $env:KUBE_CONTEXT } else { "docker-desktop" }

Write-Host "==> Verifying Docker Desktop Kubernetes is reachable (context: $Context)"
kubectl config use-context $Context | Out-Null
kubectl get nodes | Out-Null

Write-Host "==> Uninstalling Helm releases"
function Uninstall-IfPresent {
    param([string]$Release, [string]$Namespace)
    helm status $Release -n $Namespace *> $null
    if ($LASTEXITCODE -eq 0) {
        helm uninstall $Release -n $Namespace
    } else {
        Write-Host "Release '$Release' not found in namespace '$Namespace', skipping."
    }
}
Uninstall-IfPresent -Release "loki" -Namespace "monitoring"
Uninstall-IfPresent -Release "kube-prometheus-stack" -Namespace "monitoring"
Uninstall-IfPresent -Release "ingress-nginx" -Namespace "ingress-nginx"
Uninstall-IfPresent -Release "redis" -Namespace "platform"
Uninstall-IfPresent -Release "rabbitmq" -Namespace "platform"
Uninstall-IfPresent -Release "postgres" -Namespace "platform"

Write-Host "==> Deleting namespaces (and their PVCs)"
kubectl delete namespace platform monitoring ingress-nginx --ignore-not-found --wait

Write-Host "==> Teardown complete"
