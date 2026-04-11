# Lab 03 – Multiplayer Snake with Microsoft Orleans

This lab extends the single-player snake game from Lab 02 into a real-time multiplayer experience using **Microsoft Orleans** for distributed grain state management.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Azure Container Apps                      │
│                                                             │
│  ┌─────────────┐    ┌──────────────┐    ┌───────────────┐  │
│  │  Frontend   │───▶│  Backend API │───▶│  Orleans Host │  │
│  │  (Blazor)   │    │ Orleans Client│    │  (Silo + UI)  │  │
│  │  external   │    │   internal   │    │   external    │  │
│  └─────────────┘    └──────────────┘    └───────────────┘  │
│                                                      │       │
│                                              Azure Storage   │
│                                             (Clustering)     │
└─────────────────────────────────────────────────────────────┘
```

### Components

| Project | Purpose |
|---------|---------|
| `UltimateSnake.Frontend` | Blazor Web App (server-side rendering + WASM proxy) |
| `UltimateSnake.Frontend.Client` | Blazor WebAssembly game client |
| `UltimateSnake.Backend.Api` | Minimal API acting as an Orleans client |
| `UltimateSnake.Orleans.Host` | Orleans silo hosting game grains |
| `UltimateSnake.AppHost` | .NET Aspire orchestration (local dev only) |
| `UltimateSnake.ServiceDefaults` | Shared Aspire service defaults |

### Orleans Grains

| Grain | Key | Responsibility |
|-------|-----|----------------|
| `PlayerGrain` | Guid | Stores player name |
| `GameRoomGrain` | String (room code) | Manages all snakes, food position, game speed in a room |

### Game Flow

1. **Enter name** → creates a `PlayerGrain` in Orleans
2. **Create room** → generates a 6-character room code (e.g. `ABC123`), creates a `GameRoomGrain`
3. **Share room code** → other players join via the same code
4. **Each client ticks itself** at the speed provided by the `GameRoomGrain` (default: 200ms)
5. **Every tick**: client advances its own snake, sends state to API, receives full room state (all snakes + food)
6. **Food** is managed server-side in the `GameRoomGrain`; regenerated when any snake eats it

## Running Locally with Aspire

```bash
cd lab-project/lab-03/src/UltimateSnake.AppHost
dotnet run
```

Open the Aspire dashboard (URL shown in terminal). The Frontend, API, and Orleans Host all start automatically.

> Orleans uses localhost clustering when running via Aspire.

## Building Docker Images

From the `lab-project/lab-03/src` folder:

```bash
docker build -t ultimate-snake-lab03-orleans-host:latest -f UltimateSnake.Orleans.Host/Dockerfile UltimateSnake.Orleans.Host/
docker build -t ultimate-snake-lab03-api:latest -f UltimateSnake.Backend.Api/Dockerfile .
docker build -t ultimate-snake-lab03-frontend:latest -f UltimateSnake.Frontend/Dockerfile .
```

> The API Dockerfile copies the Orleans Host project alongside the API to resolve the project reference.

## Deploying to Azure Container Apps

### Prerequisites

- Azure CLI (`az`)
- A resource group, Container App Environment, and Azure Container Registry
- An Azure Storage account for Orleans clustering

### Push images to ACR

```bash
az acr login --name <your-registry>
docker tag ultimate-snake-lab03-orleans-host:latest <your-registry>.azurecr.io/ultimate-snake-lab03-orleans-host:latest
docker tag ultimate-snake-lab03-api:latest <your-registry>.azurecr.io/ultimate-snake-lab03-api:latest
docker tag ultimate-snake-lab03-frontend:latest <your-registry>.azurecr.io/ultimate-snake-lab03-frontend:latest
docker push <your-registry>.azurecr.io/ultimate-snake-lab03-orleans-host:latest
docker push <your-registry>.azurecr.io/ultimate-snake-lab03-api:latest
docker push <your-registry>.azurecr.io/ultimate-snake-lab03-frontend:latest
```

### Deploy with Bicep

```bash
az deployment group create \
  --resource-group <your-resource-group> \
  --template-file deployment/main.bicep \
  --parameters \
    frontendAppName=ultimate-snake-lab03-frontend \
    apiAppName=ultimate-snake-lab03-api \
    orleansHostAppName=ultimate-snake-lab03-orleans \
    containerAppEnvironmentName=<your-aca-environment> \
    containerRegistryName=<your-registry> \
    frontendContainerImage=<your-registry>.azurecr.io/ultimate-snake-lab03-frontend:latest \
    apiContainerImage=<your-registry>.azurecr.io/ultimate-snake-lab03-api:latest \
    orleansHostContainerImage=<your-registry>.azurecr.io/ultimate-snake-lab03-orleans-host:latest \
    managedIdentityName=ultimate-snake-lab03-identity \
    storageConnectionString=<your-azure-storage-connection-string>
```

### What the Bicep creates

| Resource | Details |
|----------|---------|
| User-Assigned Managed Identity | For ACR pull access |
| AcrPull role assignment | Grants identity access to pull images |
| Container App Environment | Consumption workload profile |
| Orleans Host Container App | External ingress (dashboard), 1–3 replicas |
| API Container App | Internal ingress, 0–10 replicas |
| Frontend Container App | External ingress, 0–10 replicas |

### Parameters

| Parameter | Description |
|-----------|-------------|
| `frontendAppName` | Name for the frontend Container App |
| `apiAppName` | Name for the API Container App |
| `orleansHostAppName` | Name for the Orleans Host Container App |
| `containerAppEnvironmentName` | Existing Container App Environment name |
| `containerRegistryName` | ACR name (without `.azurecr.io`) |
| `frontendContainerImage` | Fully qualified frontend image |
| `apiContainerImage` | Fully qualified API image |
| `orleansHostContainerImage` | Fully qualified Orleans Host image |
| `managedIdentityName` | Name for the managed identity to create |
| `storageConnectionString` | Azure Storage connection string for Orleans clustering (secure) |
| `location` | Azure region (defaults to resource group location) |

## Orleans Dashboard

After deployment, navigate to `https://<orleans-host-fqdn>/dashboard` to view grain activity, silo health, and cluster membership.
