#!/usr/bin/env bash
# Counterpart to bootstrap.sh. The tutorial's teardown deletes the kind
# cluster itself (`kind delete cluster`); since this project runs against
# Docker Desktop's built-in Kubernetes instead (see the "Local Cluster"
# note in docs/Deployment-Strategy.md), there is no cluster to delete —
# tearing down means removing everything bootstrap.sh provisioned inside
# it: the Helm releases, then the namespaces (which also cleans up their
# PVCs, since the default StorageClass's reclaim policy is Delete).
set -euo pipefail

CONTEXT="${KUBE_CONTEXT:-docker-desktop}"

echo "==> Verifying Docker Desktop Kubernetes is reachable (context: $CONTEXT)"
kubectl config use-context "$CONTEXT" >/dev/null
kubectl get nodes >/dev/null

echo "==> Uninstalling Helm releases"
uninstall_if_present() {
  local release="$1" namespace="$2"
  if helm status "$release" -n "$namespace" >/dev/null 2>&1; then
    helm uninstall "$release" -n "$namespace"
  else
    echo "Release '$release' not found in namespace '$namespace', skipping."
  fi
}
uninstall_if_present loki monitoring
uninstall_if_present kube-prometheus-stack monitoring
uninstall_if_present ingress-nginx ingress-nginx
uninstall_if_present redis platform
uninstall_if_present rabbitmq platform
uninstall_if_present postgres platform

echo "==> Deleting namespaces (and their PVCs)"
kubectl delete namespace platform monitoring ingress-nginx --ignore-not-found --wait

echo "==> Teardown complete"
