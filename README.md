# Distributed Sensor Monitoring

Distributed sensor monitoring system built with ASP.NET Core 8 microservices, PostgreSQL, and Docker.

## Overview

This project implements a fault-tolerant sensor data ingestion pipeline with consensus calculation, alarm handling, and real-time notifications. The solution is organized as multiple services sharing common contracts and data access libraries.


| Service / Library              | Description                                                        |
| ------------------------------ | ------------------------------------------------------------------ |
| **GatewayService**             | YARP reverse proxy / ingress: single public entry point routing `/api/ingest` and `/hub` |
| **IngestionService**           | Receives sensor readings via REST API, fault-tolerance pool worker |
| **SensorSimulator**            | Console client that simulates sensor readings and client-side alarms |
| **ConsensusService**           | Background worker: per-minute consensus calculation and malicious-sensor (BFT) detection |
| **NotificationService**        | Real-time alarm notifications: receives alarms from IngestionService, broadcasts over SignalR, prints color-coded console |
| **SensorMonitoring.Contracts** | Shared DTOs and enums (`SensorMessage`, `AlarmPayload`, `SecureEnvelope`, etc.) |
| **SensorMonitoring.Data**      | EF Core entities, DbContext, and migrations                        |
| **SensorMonitoring.Security**  | Reusable message encryption (AES-256-GCM + RSA) and signing (ECDSA) used by simulator and server |

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [EF Core CLI tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) (for applying migrations):
  ```bash
  dotnet tool install --global dotnet-ef
  ```

## Quick start

From the repository root:

```bash
# 1. Clone the repository
git clone <repository-url>
cd distributed-sensor-monitoring

# 2. Build the solution
dotnet build

# 3. Start PostgreSQL
docker compose up -d postgres

# 4. Apply database migrations
dotnet ef database update --project src/SensorMonitoring.Data --startup-project src/IngestionService

# 5. Generate cryptographic keys (one-time)
dotnet run --project src/SensorSimulator -- --keygen

# 6. Start all services
docker compose up --build
```

Verify containers are running:

```bash
docker compose ps
```

Check health endpoints:

```bash
curl http://localhost:5001/health   # IngestionService → 200 OK
curl http://localhost:5003/health   # NotificationService → 200 OK
```

On Windows PowerShell, use `Invoke-WebRequest` instead of `curl`:

```powershell
(Invoke-WebRequest -Uri http://localhost:5001/health -UseBasicParsing).StatusCode
(Invoke-WebRequest -Uri http://localhost:5003/health -UseBasicParsing).StatusCode
```

## Local development

Run PostgreSQL in Docker, then start a service on the host machine:

```bash
docker compose up -d postgres
dotnet ef database update --project src/SensorMonitoring.Data --startup-project src/IngestionService
dotnet run --project src/IngestionService
```

IngestionService listens on `http://localhost:5288` by default (see `src/IngestionService/Properties/launchSettings.json`). Swagger UI is available at `/swagger` in Development.

To run other services locally:

```bash
dotnet run --project src/NotificationService
dotnet run --project src/ConsensusService
```

## Phase 2: Sensor simulation & fault tolerance

Phase 2 implements the sensor data pipeline: simulated clients send readings to IngestionService, which persists them and maintains a pool of exactly 5 active sensors.

### Flow

1. **SensorSimulator** generates a random temperature every 1–10 seconds, detects alarm thresholds locally, and `POST`s a `SensorMessage` to IngestionService.
2. **IngestionService** validates the message, writes a `SensorReading` (and `AlarmEvent` when an alarm is present), and updates `Sensor.LastMessageAt`.
3. **SensorPoolWorker** runs every 2 seconds: sensors silent for more than 10 seconds are marked inactive; standby sensors are promoted to keep 5 active in the database.

### IngestionService API

| Method | Endpoint | Description |
| ------ | -------- | ----------- |
| `GET` | `/health` | Health check → `200 OK` |
| `POST` | `/api/readings` | Accept a `SensorMessage` → `202 Accepted` |
| `POST` | `/api/sensors/{id}/block` | Block a sensor for 30 seconds (testing) → `200 OK` |

Example `SensorMessage` body:

```json
{
  "sensorId": "SENSOR-001",
  "temperatureValue": 22.5,
  "timestamp": "2026-06-08T12:00:00Z",
  "messageId": 1,
  "dataQuality": "Good",
  "alarmPriority": null
}
```

Swagger UI (`/swagger`) is available when running IngestionService locally in Development.

### Sensor simulator

Run one simulator process per sensor ID (separate terminals):

```bash
dotnet run --project src/SensorSimulator -- --sensor-id SENSOR-001
dotnet run --project src/SensorSimulator -- --sensor-id SENSOR-002
dotnet run --project src/SensorSimulator -- --sensor-id SENSOR-003
dotnet run --project src/SensorSimulator -- --sensor-id SENSOR-004
dotnet run --project src/SensorSimulator -- --sensor-id SENSOR-005
```

The simulator reads `IngestionBaseUrl` from `src/SensorSimulator/appsettings.json` (default `http://localhost:5001/api` for Docker) and posts to `<IngestionBaseUrl>/readings`. The base URL must include the ingestion API prefix (`/api` when talking directly to IngestionService, `/api/ingest` when going through the gateway). If you run IngestionService locally on port `5288`, change that URL to `http://localhost:5288/api`.

Successful sends print `OK (202)`. Alarm readings are color-coded in the console: yellow (priority 1), orange (priority 2), red (priority 3).

Sensor metadata (temperature range, thresholds) is defined in `src/SensorSimulator/SensorConfig.cs` and matches the seeded data in the database (`SENSOR-001`–`007`; 5 active, 2 standby).

### Fault tolerance

| Setting | Default | Config key |
| ------- | ------- | ---------- |
| Inactivity timeout | 10 s | `FaultTolerance:InactivityTimeoutSeconds` |
| Pool worker interval | 2 s | hardcoded in `SensorPoolWorker` |
| Target active sensors | 5 | hardcoded in `SensorPoolService` |
| Manual block duration | 30 s | `POST /api/sensors/{id}/block` |

A sensor is deactivated only after it has sent at least one message and then goes silent for longer than the inactivity timeout. Promoting a standby updates `IsActive` in the database only — you must start a simulator for the promoted sensor ID (e.g. `SENSOR-006`) in a new terminal if you want it to send data.

### Phase 2 quick test

From the repository root:

```bash
docker compose up -d postgres
dotnet ef database update --project src/SensorMonitoring.Data --startup-project src/IngestionService
docker compose up --build ingestion-service
```

In new terminals:

```bash
# Health check (PowerShell)
(Invoke-WebRequest -Uri http://localhost:5001/health -UseBasicParsing).StatusCode   # → 200

# Start simulators (one per terminal)
dotnet run --project src/SensorSimulator -- --sensor-id SENSOR-001
```

Block a sensor (PowerShell):

```powershell
Invoke-WebRequest -Uri http://localhost:5001/api/sensors/SENSOR-001/block -Method POST -UseBasicParsing
```

Failover test: stop one simulator, wait ~12 seconds, then start a standby:

```bash
dotnet run --project src/SensorSimulator -- --sensor-id SENSOR-006
```

## Phase 3: Consensus & BFT

Phase 3 adds the **ConsensusService** worker: once per minute it reads the previous minute's `GOOD`-quality readings, detects malicious sensors (statistical outliers), marks them `BAD` (`Sensor.DataQuality`), and writes a consensus value to the `ConsensusValues` table.

### Migration required

This phase adds `SensorCount` / `SampleCount` columns and a unique index to `ConsensusValues`. Apply the latest migrations before running:

```bash
dotnet ef database update --project src/SensorMonitoring.Data --startup-project src/IngestionService
```

### Running

Start PostgreSQL and IngestionService (see Phase 2), run at least 3 sensor simulators, then start the worker:

```bash
dotnet run --project src/ConsensusService
```

The worker uses the connection string in `src/ConsensusService/appsettings.json` (local dev) or the `ConnectionStrings__Default` env var (Docker). In Docker it runs automatically as the `consensus-service` container. It logs one line per completed minute, e.g.:

```
Consensus 19.84°C for [12:04-12:05) from 5 sensors / 41 samples
```

### Configuration

Outlier-detection thresholds are in the `Consensus` section of `src/ConsensusService/appsettings.json`:

| Setting | Default | Meaning |
| ------- | ------- | ------- |
| `MinAbsoluteDeviation` | `10.0` | Min deviation (°C) from the population median to consider a sensor an outlier |
| `MadZScoreThreshold` | `2.5` | Modified z-score (MAD-based) above which a sensor is flagged malicious |
| `MinSensorsForDetection` | `3` | Minimum reporting sensors before detection runs (below this, consensus is still computed) |
| `GraceSeconds` | `5` | Delay after each minute boundary before processing, to tolerate late readings |

### Testing malicious-sensor detection

Run a simulator with the `--malicious` flag — it sends pinned near-maximum values (a clean outlier):

```bash
dotnet run --project src/SensorSimulator -- --sensor-id SENSOR-005 --malicious
```

Within 1–2 cycles the worker logs `Outliers detected: SENSOR-005` and `marked MALICIOUS`; that sensor's `DataQuality` becomes `BAD` and it is dropped from consensus. The mark is **permanent** — reset it to re-run the demo:

```bash
# PowerShell (pipe SQL via stdin so quoted identifiers survive)
'UPDATE "Sensors" SET "DataQuality"=''Good'' WHERE "Id"=''SENSOR-005'';' | docker exec -i git-postgres-1 psql -U snus -d sensordb
```
## Phase 3: Notifications & SignalR

The **NotificationService** delivers alarms to operators in real time. When
IngestionService persists a reading that crosses an alarm threshold, it pushes
an `AlarmPayload` to NotificationService over HTTP (best-effort — a notification
outage never blocks or fails ingestion). NotificationService then:

1. Broadcasts an `AlarmNotification` (sensor ID, value, priority, color) to all
   connected SignalR clients on the `AlarmRaised` event.
2. Prints the alarm to its own console, color-coded by priority (yellow =
   priority 1, orange = priority 2, red = priority 3).

### Endpoints

| Method | Endpoint | Description |
| ------ | -------- | ----------- |
| `GET`  | `/health` | Health check → `200 OK` |
| `POST` | `/api/alarms` | Accept an `AlarmPayload` from IngestionService → `202 Accepted` |
| (hub)  | `/hub/alarms` | SignalR hub; clients subscribe to the `AlarmRaised` event |

### Running

```bash
docker compose up -d postgres
docker compose up --build ingestion-service notification-service
```

IngestionService finds NotificationService via `NotificationService:BaseUrl`
(`src/IngestionService/appsettings.json` for local dev, the
`NotificationService__BaseUrl` env var inside Docker Compose).

## Phase 4: Security & reliable communication

Phase 4 secures the sensor → server channel. Every message is **AES-256-GCM
encrypted** (fresh key per message, RSA-wrapped for the server) and
**ECDSA-signed** per sensor; the server verifies the signature before any
processing. Replays are rejected via a timestamp window plus a strictly
increasing per-sensor message ID, and a per-sensor rate limit (10 req/s)
blocks flooding sensors. Full design and threat analysis:
[docs/SECURITY.md](docs/SECURITY.md).

### Migration required

This phase adds the `Sensors.LastMessageId` column (replay protection):

```bash
dotnet ef database update --project src/SensorMonitoring.Data --startup-project src/IngestionService
```

### Key generation (one-time setup)

Keys are **not** committed to the repository. Generate them once from the
repo root:

```bash
dotnet run --project src/SensorSimulator -- --keygen
```

This writes a git-ignored `keys/` folder: `keys/server/` (server RSA private
key + sensor public keys — mounted into the ingestion container by
docker-compose) and `keys/client/` (server RSA public key + sensor private
keys — used by simulators; copy this folder to the client machine for the
two-machine demo).

### Endpoint contract change

`POST /api/readings` now accepts a `SecureEnvelope` (encrypted + signed)
instead of a plain `SensorMessage`, plus a plaintext `X-Sensor-Id` header
used by the rate limiter. Responses:

| Status | Meaning |
| ------ | ------- |
| `202`  | Reading accepted |
| `400`  | Malformed envelope / decryption failed / header–envelope ID mismatch |
| `401`  | Signature verification failed (unknown sensor key or invalid signature) |
| `409`  | Replay detected (stale timestamp or already-seen MessageId), or sensor blocked/inactive |
| `429`  | Rate limit exceeded (more than 10 req/s from one sensor ID) |

### Security test modes (simulator flags)

```bash
# Normal operation - readings accepted (202)
dotnet run --project src/SensorSimulator -- --sensor-id SENSOR-001

# Tampered signature - every message rejected (401), nothing persisted
dotnet run --project src/SensorSimulator -- --sensor-id SENSOR-002 --bad-signature

# Replay attack - each envelope sent twice; duplicate rejected (409)
dotnet run --project src/SensorSimulator -- --sensor-id SENSOR-003 --replay

# DoS flood (~20 msg/s) - first 10 accepted, then 429, then the sensor is
# blocked for 30 s (409) and a standby sensor is promoted
dotnet run --project src/SensorSimulator -- --sensor-id SENSOR-004 --flood
```

Rate-limit settings live in the `RateLimiting` section of
`src/IngestionService/appsettings.json` (`PermitLimit`, `WindowSeconds`);
the replay timestamp window is `Security:TimestampToleranceSeconds`.

### Two-machine setup (defense demo)

Server machine:

```powershell
# 1. Find the LAN IP (IPv4 address)
ipconfig

# 2. Allow inbound traffic on the ingestion port
New-NetFirewallRule -DisplayName "SNUS ingestion" -Direction Inbound -Protocol TCP -LocalPort 5001 -Action Allow

# 3. Generate keys and start the stack (binds to all interfaces)
dotnet run --project src/SensorSimulator -- --keygen
docker compose up --build
```

Client machine (needs the .NET 8 SDK and a copy of the repo):

```powershell
# 1. Copy the keys/client folder from the server machine into .\keys\client

# 2. Point the simulator at the server and run
$env:IngestionBaseUrl = "http://<SERVER-LAN-IP>:5001/api"
dotnet run --project src/SensorSimulator -- --sensor-id SENSOR-001
```

Note: replay protection compares message timestamps with the server clock
(±30 s tolerance), so both machines should have correct time (NTP — the
Windows default — is sufficient).

## Phase 5: Ingress / API gateway

The **GatewayService** is a [YARP](https://microsoft.github.io/reverse-proxy/)
reverse proxy that fronts the system as a single public entry point, so clients
no longer talk to each service's port directly. Routing is path-based and
config-driven (`src/GatewayService/appsettings.json`):

| Public route | Forwarded to | Notes |
| ------------ | ------------ | ----- |
| `/api/ingest/**` | IngestionService | The `/api/ingest` prefix is rewritten to `/api`, so `/api/ingest/readings` → `/api/readings` and `/api/ingest/sensors/{id}/block` → `/api/sensors/{id}/block` |
| `/hub/**` | NotificationService | SignalR hub (`/hub/alarms`); YARP forwards the WebSocket upgrade transparently |
| `/health` | — | The gateway's own health check → `200 OK` |

`/api/reports` is intentionally **not** routed — there is no reporting service in
this project.

### Running

In Docker Compose the gateway runs as the `gateway` service and is the only
service that needs to be published. It depends on `ingestion-service` and
`notification-service`:

```bash
docker compose up --build
```

```powershell
# Gateway health (PowerShell)
(Invoke-WebRequest -Uri http://localhost:8080/health -UseBasicParsing).StatusCode   # → 200
```

Requests then flow through the gateway, e.g. `http://localhost:8080/api/ingest/readings`
reaches IngestionService and `ws://localhost:8080/hub/alarms` reaches the SignalR hub.

## Phase 5: Kubernetes (Minikube)

The system also runs on Kubernetes, mirroring the Docker Compose topology. All
manifests live in [`k8s/`](k8s/): a `Deployment` + `Service` + `ConfigMap` /
`Secret` per microservice, a `PersistentVolumeClaim` for PostgreSQL, and a
one-shot `Job` that applies EF Core migrations. Everything is isolated in the
`snus` namespace.

| Manifest | Resources |
| -------- | --------- |
| `k8s/namespace.yaml` | `Namespace` `snus` |
| `k8s/postgres.yaml` | `Secret` `db-credentials`, `PersistentVolumeClaim` `pgdata`, PostgreSQL `Deployment` + `Service` (`postgres:5432`) |
| `k8s/config.yaml` | shared `ConfigMap` `app-config` (ASPNETCORE URLs, notification base URL, key paths) |
| `k8s/migrate-job.yaml` | `Job` `db-migrate` that runs `dotnet ef database update` |
| `k8s/ingestion.yaml` | IngestionService `Deployment` + `Service` (mounts the crypto-key Secrets) |
| `k8s/notification.yaml` | NotificationService `Deployment` + `Service` |
| `k8s/consensus.yaml` | ConsensusService `Deployment` (background worker, no Service) |
| `k8s/gateway.yaml` | GatewayService `Deployment` + `NodePort` `Service` (`30080` → `8080`) |
| `k8s/ingress.yaml` | optional host-based `Ingress` (`snus.local`) for the gateway |

The Service names (`postgres`, `ingestion-service`, `notification-service`)
match the hostnames already baked into config, so no application changes are
needed.

### Prerequisites

- [minikube](https://minikube.sigs.k8s.io/docs/start/) and
  [kubectl](https://kubernetes.io/docs/tasks/tools/)
- Crypto keys generated once (see Phase 4): `dotnet run --project src/SensorSimulator -- --keygen`

Start the cluster:

```powershell
minikube start
```

### 1. Build the images into Minikube

Minikube has its own image store. Point Docker at it and build there, so no
registry push is required (every Deployment uses `imagePullPolicy: IfNotPresent`):

```powershell
minikube docker-env | Invoke-Expression
docker build -t snus/ingestion:latest    -f src/IngestionService/Dockerfile .
docker build -t snus/notification:latest -f src/NotificationService/Dockerfile .
docker build -t snus/consensus:latest    -f src/ConsensusService/Dockerfile .
docker build -t snus/gateway:latest      -f src/GatewayService/Dockerfile .
docker build -t snus/migrate:latest      -f k8s/Dockerfile.migrate .
```

On Linux/macOS use `eval $(minikube docker-env)` instead of the first line.

### 2. Create the crypto-key Secrets

The crypto keys are git-ignored, so their Secrets are created imperatively from
the on-disk `keys/` folder (not committed as YAML). A helper script handles both
Secrets (and ensures the namespace exists):

```powershell
./k8s/create-secrets.ps1   # PowerShell
./k8s/create-secrets.sh    # Linux/macOS
```

This creates `server-private-key` (from `keys/server/server_rsa_private.pem`)
and `sensor-public-keys` (from `keys/server/sensors/`) in the `snus` namespace.
The `db-credentials` Secret, by contrast, ships in `k8s/postgres.yaml` (demo
password only).

### 3. Apply the manifests (in order)

```powershell
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/postgres.yaml
kubectl rollout status deploy/postgres -n snus

kubectl apply -f k8s/config.yaml
kubectl apply -f k8s/migrate-job.yaml
kubectl wait --for=condition=complete job/db-migrate -n snus --timeout=120s

kubectl apply -f k8s/ingestion.yaml -f k8s/notification.yaml -f k8s/consensus.yaml -f k8s/gateway.yaml
```

Check everything is running:

```powershell
kubectl get pods -n snus
```

### 4. Reach the gateway

The gateway `Service` is a `NodePort` exposed on `30080`. The simplest,
cross-platform way to reach it (and the one that also works for the two-machine
demo on the Windows docker driver, where the node IP is not LAN-reachable) is a
port-forward bound to all interfaces:

```powershell
kubectl port-forward --address 0.0.0.0 svc/gateway 8080:8080 -n snus
```

Then:

```powershell
# Gateway health
(Invoke-WebRequest -Uri http://localhost:8080/health -UseBasicParsing).StatusCode   # → 200
```

Requests flow through the gateway exactly as in Docker Compose, e.g.
`http://localhost:8080/api/ingest/readings`. Point the simulator's
`IngestionBaseUrl` at the gateway including the `/api/ingest` prefix (the gateway
rewrites it to `/api/readings` for IngestionService):

```powershell
# Same machine, via the port-forward above
$env:IngestionBaseUrl = "http://localhost:8080/api/ingest"
dotnet run --project src/SensorSimulator -- --sensor-id SENSOR-001
```

For the two-machine demo, use the server's LAN IP instead:

```powershell
$env:IngestionBaseUrl = "http://<SERVER-LAN-IP>:8080/api/ingest"
dotnet run --project src/SensorSimulator -- --sensor-id SENSOR-001
```

Alternatively, `minikube service gateway -n snus --url` prints the NodePort URL,
or enable the optional Ingress with `minikube addons enable ingress`, apply
`k8s/ingress.yaml`, and add `<minikube-ip> snus.local` to your hosts file.

### Teardown

```powershell
kubectl delete namespace snus
```

## Database

PostgreSQL 16 runs in Docker with these defaults:


| Setting               | Value       |
| --------------------- | ----------- |
| Host (local dev)      | `localhost` |
| Host (inside compose) | `postgres`  |
| Port                  | `5432`      |
| Database              | `sensordb`  |
| Username              | `snus`      |
| Password              | `snus`      |


### Connection strings


| Context                       | Connection string                                                        |
| ----------------------------- | ------------------------------------------------------------------------ |
| Local dev (`dotnet run`)      | `Host=localhost;Port=5432;Database=sensordb;Username=snus;Password=snus` |
| Inside Docker Compose network | `Host=postgres;Port=5432;Database=sensordb;Username=snus;Password=snus`  |


The local connection string is configured in `src/IngestionService/appsettings.json` and `src/ConsensusService/appsettings.json`. Docker services receive the compose-network string via the `ConnectionStrings__Default` environment variable.

### Schema and seed data

EF Core migrations create four tables: `Sensors`, `SensorReadings`, `ConsensusValues`, and `AlarmEvents`. The initial migration seeds 7 sensors (5 active, 2 standby).

Apply or update the schema:

```bash
dotnet ef database update --project src/SensorMonitoring.Data --startup-project src/IngestionService
```

Add a new migration after entity changes:

```bash
dotnet ef migrations add <MigrationName> --project src/SensorMonitoring.Data --startup-project src/IngestionService
```

## Docker

### Service ports


| Service             | Host port                                      | Health check  |
| ------------------- | ---------------------------------------------- | ------------- |
| PostgreSQL          | 5432                                           | —             |
| GatewayService      | [http://localhost:8080](http://localhost:8080) | `GET /health` |
| IngestionService    | [http://localhost:5001](http://localhost:5001) | `GET /health` |
| NotificationService | [http://localhost:5003](http://localhost:5003) | `GET /health` |
| ConsensusService    | — (background worker, no public port)          | —             |


### Common commands

```bash
# Start only the database
docker compose up -d postgres

# Build and start the full stack (foreground)
docker compose up --build

# Build and start the full stack (detached)
docker compose up --build -d

# Stop all containers
docker compose down

# Stop containers and remove the database volume
docker compose down -v
```

