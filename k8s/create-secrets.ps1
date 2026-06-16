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
#   ./k8s/create-secrets.ps1
#
# Idempotent: re-running updates the Secrets in place.

$ErrorActionPreference = "Stop"

# Resolve paths relative to the repo root (this script lives in k8s/).
$repoRoot = Split-Path -Parent $PSScriptRoot
$privateKey = Join-Path $repoRoot "keys/server/server_rsa_private.pem"
$publicKeysDir = Join-Path $repoRoot "keys/server/sensors"
$namespace = "snus"

if (-not (Test-Path $privateKey)) {
    throw "Missing $privateKey. Run: dotnet run --project src/SensorSimulator -- --keygen"
}
if (-not (Test-Path $publicKeysDir)) {
    throw "Missing $publicKeysDir. Run: dotnet run --project src/SensorSimulator -- --keygen"
}

# Ensure the namespace exists before creating namespaced Secrets.
kubectl apply -f (Join-Path $PSScriptRoot "namespace.yaml")

# `create --dry-run | apply` makes both Secrets idempotent (create or update).
kubectl create secret generic server-private-key -n $namespace `
    --from-file=server_rsa_private.pem=$privateKey `
    --dry-run=client -o yaml | kubectl apply -f -

kubectl create secret generic sensor-public-keys -n $namespace `
    --from-file=$publicKeysDir `
    --dry-run=client -o yaml | kubectl apply -f -

Write-Host "Created/updated Secrets 'server-private-key' and 'sensor-public-keys' in namespace '$namespace'."
