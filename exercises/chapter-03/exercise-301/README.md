[← Back to Exercises](../../README.md) · [↑ View Slide](../../../content/chapter-03/README.md)

# Exercise 301 — Stateful Scalable Services with Microsoft Orleans

In this exercise you will build an Orleans cluster deployed to Azure Container Apps. The solution consists of three services:

| Service | Role |
|---------|------|
| **Orleans Host** | Silo that hosts Orleans grains and exposes the Orleans Dashboard |
| **Api** | Minimal API acting as an Orleans client — calls a `StatusGrain` and returns the result |
| **Worker** | Load generator that hammers the Api endpoint to demonstrate auto-scaling |

## What you'll learn

- How to host a Microsoft Orleans silo inside an ASP.NET Core application
- How to connect an Orleans client (the Api) to the silo cluster
- Switching between `UseLocalhostClustering` (Aspire/local) and Azure Table Storage clustering (Docker/ACA) using a compile-time `#if ASPIRE` flag
- Running multiple services together with Aspire for local development
- Deploying a multi-service Orleans workload to Azure Container Apps with Bicep

## Solution Structure

```
exercise-301/
├── AzureContainerApps.Exercise301.Orleans.Host/   # Silo + Orleans Dashboard
│   ├── Grains/
│   │   ├── IStatusGrain.cs
│   │   └── StatusGrain.cs
│   ├── Program.cs
│   └── Dockerfile
├── AzureContainerApps.Exercise301.Api/            # Orleans client + REST endpoint
│   ├── Program.cs
│   └── Dockerfile
├── AzureContainerApps.Exercise301.Worker/         # Load generator
│   ├── Program.cs
│   └── Dockerfile
├── AzureContainerApps.Exercise301.AppHost/        # Aspire AppHost (local dev only)
│   └── AppHost.cs
├── AzureContainerApps.Exercise301.ServiceDefaults/ # Shared Aspire service defaults
│   └── Extensions.cs
├── deployment/
│   ├── main.bicep
│   └── roleAssignment.bicep
├── docker-compose.yml
└── README.md
```

## Key Concepts

### StatusGrain
The grain is uniquely identified by a `Guid` key and returns the silo's machine name together with its own grain ID:

```csharp
[GenerateSerializer]
public record StatusResult([property: Id(0)] string MachineName, [property: Id(1)] Guid GrainId);

public class StatusGrain : Grain, IStatusGrain
{
    public Task<StatusResult> GetStatusAsync()
        => Task.FromResult(new StatusResult(Environment.MachineName, this.GetPrimaryKey()));
}
```

### Clustering strategy
The Orleans Host and the Api use a **compile-time** flag (`#if ASPIRE`) to choose their clustering mode:

- **`ASPIRE` defined** (built with `UseAspire=true`, the default) → `UseLocalhostClustering()` — all processes run on the same machine under Aspire.
- **`ASPIRE` not defined** (built with `/p:UseAspire=false`, used by Docker and Azure builds) → `UseAzureStorageClustering()` using `AZURE_STORAGE_CONNECTION_STRING`.

The Dockerfiles pass `/p:UseAspire=false` to `dotnet publish` so the container images always use Azure Storage clustering.

### Orleans Dashboard (Microsoft.Orleans.Dashboard)
Orleans 10 ships with an official first-party dashboard package (`Microsoft.Orleans.Dashboard`). It is registered on the silo builder and served through the ASP.NET Core pipeline at the `/dashboard` route prefix:

```csharp
// In silo setup
using Orleans.Dashboard;
siloBuilder.AddDashboard();

// In ASP.NET Core pipeline
app.MapOrleansDashboard(routePrefix: "/dashboard");
```

```
https://<orleans-host-fqdn>/dashboard
```

## Running Locally with Aspire

> Prerequisites: .NET Aspire workload installed (`dotnet workload install aspire`)

```bash
cd AzureContainerApps.Exercise301.AppHost
dotnet run
```

Aspire starts all three services using `UseLocalhostClustering`. Open the Aspire dashboard URL printed in the console to see all running services, logs, and traces. The Orleans Dashboard link appears under the `orleans-host` resource.

## Running with Docker Compose

Docker Compose uses **Azurite** as a local Azure Table Storage emulator so that the Orleans Host and the Api can discover each other through the table membership protocol.

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running

### 1. Build all images

Open a terminal in the `exercise-301` folder and run:

```bash
docker compose build
```

### 2. Start all containers

```bash
docker compose up
```

| Endpoint | URL |
|----------|-----|
| Orleans Dashboard | http://localhost:8080/dashboard |
| Api `/status` | http://localhost:8081/status |

### 3. Stop the containers

```bash
docker compose down
```

---

## Running manually with Docker

### 1. Create a shared network

```bash
docker network create exercise-301
```

### 2. Start Azurite (Table Storage emulator)

```bash
docker run -d --name azurite --network exercise-301 \
  mcr.microsoft.com/azure-storage/azurite \
  azurite --loose --blobHost 0.0.0.0 --queueHost 0.0.0.0 --tableHost 0.0.0.0
```

### 3. Build and run the Orleans Host

```bash
docker build -t azure-container-apps-exercise-301-orleans-host:latest \
  ./AzureContainerApps.Exercise301.Orleans.Host

docker run -d --name orleans-host --network exercise-301 -p 8080:8080 \
  -e AZURE_STORAGE_CONNECTION_STRING="DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KgMeOQ==;TableEndpoint=http://azurite:10002/devstoreaccount1;" \
  azure-container-apps-exercise-301-orleans-host:latest
```

### 4. Build and run the Api

> The Api Dockerfile requires the Orleans Host project files (shared grain interfaces), so the build context is the `exercise-301` folder.

```bash
docker build -t azure-container-apps-exercise-301-api:latest \
  -f AzureContainerApps.Exercise301.Api/Dockerfile .

docker run -d --name api --network exercise-301 -p 8081:8080 \
  -e AZURE_STORAGE_CONNECTION_STRING="DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KgMeOQ==;TableEndpoint=http://azurite:10002/devstoreaccount1;" \
  azure-container-apps-exercise-301-api:latest
```

### 5. Build and run the Worker

```bash
docker build -t azure-container-apps-exercise-301-worker:latest \
  ./AzureContainerApps.Exercise301.Worker

docker run --rm --network exercise-301 \
  -e API_BASE_URL=http://api:8080 \
  azure-container-apps-exercise-301-worker:latest
```

| Flag / Variable | Description |
|----------------|-------------|
| `--network exercise-301` | Connects all containers on the same Docker network |
| `AZURE_STORAGE_CONNECTION_STRING` | Azurite connection string for Orleans Table Storage clustering |
| `API_BASE_URL` | Points the worker at the Api container by name |

---

## Deploying to Azure Container Apps

The `deployment/main.bicep` file deploys all three containers as Azure Container Apps in a shared environment:

| Container App | Ingress | Scale |
|--------------|---------|-------|
| Orleans Host (`orleansHostAppName`) | **External** — public HTTPS, serves the Orleans Dashboard at `/dashboard` | fixed **2 replicas** |
| API (`apiAppName`) | **Internal** — reachable only within the environment | min **0**, max **10** |
| Worker (`workerAppName`) | **None** — outbound only | always **1** |

### Prerequisites

- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) installed
- Logged in to Azure (`az login`)
- An existing Azure resource group and Azure Container Registry

### 1. Push all images to your registry

Run these commands from the `exercise-301` folder:

```bash
REGISTRY=<your-registry-name>

# Orleans Host
az acr build \
  --registry $REGISTRY \
  --image azure-container-apps-exercise-301-orleans-host:latest \
  --file AzureContainerApps.Exercise301.Orleans.Host/Dockerfile \
  ./AzureContainerApps.Exercise301.Orleans.Host

# Api (build context = exercise-301 folder due to shared grain interfaces)
az acr build \
  --registry $REGISTRY \
  --image azure-container-apps-exercise-301-api:latest \
  --file AzureContainerApps.Exercise301.Api/Dockerfile \
  .

# Worker
az acr build \
  --registry $REGISTRY \
  --image azure-container-apps-exercise-301-worker:latest \
  --file AzureContainerApps.Exercise301.Worker/Dockerfile \
  ./AzureContainerApps.Exercise301.Worker
```

### 2. Run the Bicep deployment

```bash
az deployment group create \
  --resource-group <your-resource-group> \
  --template-file deployment/main.bicep \
  --parameters \
      orleansHostAppName=exercise-301-host \
      apiAppName=exercise-301-api \
      workerAppName=exercise-301-worker \
      containerAppEnvironmentName=<env-name> \
      containerRegistryName=$REGISTRY \
      managedIdentityName=exercise-301-identity \
      storageConnectionString="<your-storage-connection-string>" \
      orleansHostContainerImage=$REGISTRY.azurecr.io/azure-container-apps-exercise-301-orleans-host:latest \
      apiContainerImage=$REGISTRY.azurecr.io/azure-container-apps-exercise-301-api:latest \
      workerContainerImage=$REGISTRY.azurecr.io/azure-container-apps-exercise-301-worker:latest
```

| Parameter | Description |
|-----------|-------------|
| `orleansHostAppName` | Name for the Orleans Host Container App |
| `apiAppName` | Name for the API Container App |
| `workerAppName` | Name for the Worker Container App |
| `containerAppEnvironmentName` | Name for the Container App Environment (created if it doesn't exist) |
| `containerRegistryName` | Azure Container Registry name (without `.azurecr.io`) |
| `managedIdentityName` | Name of the user-assigned managed identity |
| `storageConnectionString` | Azure Storage connection string for Orleans Table Storage clustering (marked `@secure()`) |
| `orleansHostContainerImage` | Fully qualified Orleans Host image tag |
| `apiContainerImage` | Fully qualified API image tag |
| `workerContainerImage` | Fully qualified Worker image tag |
| `location` | Azure region (defaults to resource group location) |

### What the Bicep creates

- **User-assigned managed identity** with `AcrPull` role on the registry
- **Container App Environment** (Consumption workload profile)
- **Orleans Host** — external ingress, fixed **2 replicas** (stable membership required for silo-to-silo clustering)
- **Api** — internal ingress, scales **0 → 10** replicas via HTTP concurrency rule (10 concurrent requests per replica)
- **Worker** — no ingress, fixed **1 replica**, receives `API_BASE_URL` pointing to the Api

The `storageConnectionString` is stored as a Container Apps secret and injected as `AZURE_STORAGE_CONNECTION_STRING` into both the Orleans Host and Api.

### 3. View the Orleans Dashboard

After deployment the Bicep outputs `orleansHostUrl` pointing directly to the dashboard. Retrieve it with:

```bash
az deployment group show \
  --resource-group <your-resource-group> \
  --name main \
  --query properties.outputs.orleansHostUrl.value \
  --output tsv
```

---

## Scaling Behaviour

The Worker fires 50 concurrent HTTP requests per second to the Api. With the HTTP scaling rule set to 10 concurrent requests per replica, you should observe the Api scaling up to approximately 5 replicas. The Orleans cluster (2 fixed silos) distributes grain activations across both hosts.

Monitor scaling in the Azure portal under **Container Apps → Api → Revisions and replicas**.

