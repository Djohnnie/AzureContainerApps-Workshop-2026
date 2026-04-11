# Lab 04 – Multiplayer Snake with Orleans and KEDA External Scaler

This lab extends Lab 03 by adding a **custom KEDA external scaler** (`UltimateSnake.Orleans.Scaler`) that lets Azure Container Apps automatically scale the Orleans Host based on actual grain activity in the cluster.

## Architecture

```
┌──────────────────────────────────────────────────────────────────────┐
│                         Azure Container Apps                          │
│                                                                      │
│  ┌─────────────┐    ┌──────────────┐    ┌───────────────┐            │
│  │  Frontend   │───▶│  Backend API │───▶│  Orleans Host │◀── KEDA ──┐│
│  │  (Blazor)   │    │ Orleans Client│    │  1–4 replicas │           ││
│  │  external   │    │   internal   │    │   external    │           ││
│  └─────────────┘    └──────────────┘    └───────┬───────┘           ││
│                                                  │                   ││
│                                          Azure Storage               ││
│                                         (Clustering)                 ││
│                                                                      ││
│                               ┌──────────────────────────────────┐  ││
│                               │  Orleans Scaler (gRPC, HTTP/2)   │──┘│
│                               │  1 replica — always running      │   │
│                               └──────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────┘
```

### Components

| Project | Purpose |
|---------|---------|
| `UltimateSnake.Frontend` | Blazor Web App (server-side rendering + WASM proxy) |
| `UltimateSnake.Frontend.Client` | Blazor WebAssembly game client |
| `UltimateSnake.Backend.Api` | Minimal API acting as an Orleans client |
| `UltimateSnake.Orleans.Host` | Orleans silo hosting game grains |
| `UltimateSnake.Orleans.Contracts` | Shared grain interfaces (no Host↔API dependency) |
| `UltimateSnake.Orleans.Scaler` | KEDA gRPC external scaler — queries the Orleans cluster |
| `UltimateSnake.AppHost` | .NET Aspire orchestration (local dev only) |
| `UltimateSnake.ServiceDefaults` | Shared Aspire service defaults |

### How the KEDA Scaler Works

The `UltimateSnake.Orleans.Scaler` is a gRPC service implementing the [KEDA external scaler contract](https://keda.sh/docs/concepts/external-scalers/). It:

1. Connects to the Orleans cluster as a client using Azure Storage clustering
2. Calls `IManagementGrain` to count active grains and active silos
3. Exposes a single metric (`siloThreshold`) computed as:
   - `grainsPerSilo = grainCount / siloCount`
   - If `grainsPerSilo < upperbound` → scale **down** toward `max(1, grainCount / upperbound)`
   - If `grainsPerSilo >= upperbound` → scale **up** to `siloCount + 1`

KEDA compares the metric against the target of **1** and adjusts replicas accordingly. The Orleans Host scales between 1 and 4 replicas.

The scaler runs as exactly **1 replica** with HTTP/2 ingress (required for gRPC streaming).

### Orleans Grains

| Grain | Key | Responsibility |
|-------|-----|----------------|
| `PlayerGrain` | Guid | Stores player name |
| `GameRoomGrain` | String (room code) | Manages all snakes, food position, game speed in a room |

## Running Locally with Aspire

```bash
cd lab-project/lab-04/src/UltimateSnake.AppHost
dotnet run
```

> The Orleans Scaler is not part of the Aspire AppHost — it's a production-only deployment concern. Run it separately if needed.

## Running Locally with Docker Compose

From the `lab-project/lab-04/src` folder:

```bash
docker compose up --build
```

Services exposed:
| URL | Service |
|-----|---------|
| `http://localhost:8080` | Frontend |
| `http://localhost:8081` | Backend API |
| `http://localhost:8082` | Orleans Host |
| `http://localhost:8083` | Orleans Scaler (gRPC) |

## Deploying to Azure Container Apps

### Prerequisites

- Azure CLI (`az`)
- A resource group, Container App Environment, and Azure Container Registry
- An Azure Storage account for Orleans clustering

### Build Docker Images

From the `lab-project/lab-04/src` folder:

```bash
docker build -t ultimate-snake-lab04-orleans-host:latest -f UltimateSnake.Orleans.Host/Dockerfile .
docker build -t ultimate-snake-lab04-api:latest -f UltimateSnake.Backend.Api/Dockerfile .
docker build -t ultimate-snake-lab04-frontend:latest -f UltimateSnake.Frontend/Dockerfile .
docker build -t ultimate-snake-lab04-scaler:latest UltimateSnake.Orleans.Scaler/
```

### Push images to ACR

```bash
az acr login --name <your-registry>
docker tag ultimate-snake-lab04-orleans-host:latest <your-registry>.azurecr.io/ultimate-snake-lab04-orleans-host:latest
docker tag ultimate-snake-lab04-api:latest <your-registry>.azurecr.io/ultimate-snake-lab04-api:latest
docker tag ultimate-snake-lab04-frontend:latest <your-registry>.azurecr.io/ultimate-snake-lab04-frontend:latest
docker tag ultimate-snake-lab04-scaler:latest <your-registry>.azurecr.io/ultimate-snake-lab04-scaler:latest
docker push <your-registry>.azurecr.io/ultimate-snake-lab04-orleans-host:latest
docker push <your-registry>.azurecr.io/ultimate-snake-lab04-api:latest
docker push <your-registry>.azurecr.io/ultimate-snake-lab04-frontend:latest
docker push <your-registry>.azurecr.io/ultimate-snake-lab04-scaler:latest
```

### Deploy with Bicep

```bash
az deployment group create \
  --resource-group <your-resource-group> \
  --template-file deployment/main.bicep \
  --parameters \
    frontendAppName=ultimate-snake-lab04-frontend \
    apiAppName=ultimate-snake-lab04-api \
    orleansHostAppName=ultimate-snake-lab04-orleans \
    scalerAppName=ultimate-snake-lab04-scaler \
    containerAppEnvironmentName=<your-aca-environment> \
    containerRegistryName=<your-registry> \
    frontendContainerImage=<your-registry>.azurecr.io/ultimate-snake-lab04-frontend:latest \
    apiContainerImage=<your-registry>.azurecr.io/ultimate-snake-lab04-api:latest \
    orleansHostContainerImage=<your-registry>.azurecr.io/ultimate-snake-lab04-orleans-host:latest \
    scalerContainerImage=<your-registry>.azurecr.io/ultimate-snake-lab04-scaler:latest \
    managedIdentityName=ultimate-snake-lab04-identity \
    storageConnectionString=<your-azure-storage-connection-string>
```

### What the Bicep creates

| Resource | Details |
|----------|---------|
| User-Assigned Managed Identity | For ACR pull access |
| AcrPull role assignment | Grants identity access to pull images |
| Container App Environment | Consumption workload profile |
| Orleans Scaler Container App | External ingress (HTTP/2 for gRPC), exactly 1 replica |
| Orleans Host Container App | External ingress, 1–4 replicas, KEDA custom external scale rule |
| API Container App | Internal ingress, 0–10 replicas |
| Frontend Container App | External ingress, 0–10 replicas |

### Parameters

| Parameter | Description |
|-----------|-------------|
| `frontendAppName` | Name for the frontend Container App |
| `apiAppName` | Name for the API Container App |
| `orleansHostAppName` | Name for the Orleans Host Container App |
| `scalerAppName` | Name for the Orleans Scaler Container App |
| `containerAppEnvironmentName` | Existing Container App Environment name |
| `containerRegistryName` | ACR name (without `.azurecr.io`) |
| `frontendContainerImage` | Fully qualified frontend image |
| `apiContainerImage` | Fully qualified API image |
| `orleansHostContainerImage` | Fully qualified Orleans Host image |
| `scalerContainerImage` | Fully qualified Orleans Scaler image |
| `managedIdentityName` | Name for the managed identity to create |
| `storageConnectionString` | Azure Storage connection string for Orleans clustering (secure) |
| `location` | Azure region (defaults to resource group location) |

## Orleans Dashboard

After deployment, navigate to `https://<orleans-host-fqdn>/dashboard` to view grain activity, silo health, and cluster membership.

