# Distributed Sensor Monitoring

Distributed sensor monitoring system built with ASP.NET Core 8 microservices, PostgreSQL, and Docker.

## Overview

This project implements a fault-tolerant sensor data ingestion pipeline with consensus calculation, alarm handling, and real-time notifications. The solution is organized as multiple services sharing common contracts and data access libraries.


| Service / Library              | Description                                                        |
| ------------------------------ | ------------------------------------------------------------------ |
| **IngestionService**           | Receives sensor readings via REST API                              |
| **ConsensusService**           | Background worker for consensus value calculation                  |
| **NotificationService**        | Real-time alarm and status notifications (SignalR in later phases) |
| **SensorMonitoring.Contracts** | Shared DTOs and enums (`SensorMessage`, `AlarmPayload`, etc.)      |
| **SensorMonitoring.Data**      | EF Core entities, DbContext, and migrations                        |


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

# 5. Start all services
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

