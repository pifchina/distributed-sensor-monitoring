#!/usr/bin/env bash
# Creates the crypto-key Secrets the cluster needs from the git-ignored keys/ folder.
#
# These Secrets cannot ship as committed YAML because keys/ is git-ignored (the
# private key is sensitive and the public keys are generated per-deployment), so
# they are created imperatively from the on-disk key files instead.
#
#   server-private-key  <- keys/server/server_rsa_private.pem
#                          (mounted at /app/keys/server, read by
#                           Security__ServerPrivateKeyPath)
#   sensor-public-keys  <- keys/server/sensors/*.public.pem
#                          (mounted at /app/keys/server/sensors, read by
#                           Security__SensorPublicKeysDirectory)
#
# Prerequisites: run `dotnet run --project src/SensorSimulator -- --keygen` once
# to generate keys/, and have a cluster running (e.g. `minikube start`).
#
# Usage (from anywhere):
#   ./k8s/create-secrets.sh
#
# Idempotent: re-running updates the Secrets in place.

set -euo pipefail

# Resolve paths relative to the repo root (this script lives in k8s/).
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
PRIVATE_KEY="$REPO_ROOT/keys/server/server_rsa_private.pem"
PUBLIC_KEYS_DIR="$REPO_ROOT/keys/server/sensors"
NAMESPACE="snus"

if [ ! -f "$PRIVATE_KEY" ]; then
    echo "Missing $PRIVATE_KEY. Run: dotnet run --project src/SensorSimulator -- --keygen" >&2
    exit 1
fi
if [ ! -d "$PUBLIC_KEYS_DIR" ]; then
    echo "Missing $PUBLIC_KEYS_DIR. Run: dotnet run --project src/SensorSimulator -- --keygen" >&2
    exit 1
fi

# Ensure the namespace exists before creating namespaced Secrets.
kubectl apply -f "$SCRIPT_DIR/namespace.yaml"

# `create --dry-run | apply` makes both Secrets idempotent (create or update).
kubectl create secret generic server-private-key -n "$NAMESPACE" \
    --from-file=server_rsa_private.pem="$PRIVATE_KEY" \
    --dry-run=client -o yaml | kubectl apply -f -

kubectl create secret generic sensor-public-keys -n "$NAMESPACE" \
    --from-file="$PUBLIC_KEYS_DIR" \
    --dry-run=client -o yaml | kubectl apply -f -

echo "Created/updated Secrets 'server-private-key' and 'sensor-public-keys' in namespace '$NAMESPACE'."
