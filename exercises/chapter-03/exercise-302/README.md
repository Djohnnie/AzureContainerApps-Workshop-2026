[← Back to Exercises](../../README.md) · [↑ View Slide](../../../content/chapter-03/README.md)

# Exercise 302 — Passwordless Azure Storage Access with Service Connections

This exercise extends [Exercise 301](../exercise-301/README.md). It uses exactly the same Orleans cluster architecture (Orleans Host, Api, Worker) but replaces the `AZURE_STORAGE_CONNECTION_STRING` secret with **passwordless access** to Azure Storage Tables using a **user-assigned managed identity** and `DefaultAzureCredential`.

| Service | Role |
|---------|------|
| **Orleans Host** | Silo that hosts Orleans grains and exposes the Orleans Dashboard |
| **Api** | Minimal API acting as an Orleans client — calls a `StatusGrain` and returns the result |
| **Worker** | Load generator that hammers the Api endpoint to demonstrate auto-scaling |

## What you'll learn

- How to eliminate secrets from Azure Container Apps using managed identity
- How the **Azure Container Apps Service Connection** pattern works: the env-var naming convention (`AZURE_STORAGETABLE_*`) used by the Azure Service Connector
- Using `DefaultAzureCredential` with a user-assigned managed identity to access Azure Table Storage
- Assigning the `Storage Table Data Contributor` RBAC role via Bicep
- The difference between connection-string clustering (Exercise 301) and passwordless clustering (Exercise 302)

## Solution Structure

```
exercise-302/
├── AzureContainerApps.Exercise302.Orleans.Host/   # Silo + Orleans Dashboard
│   ├── Grains/
│   │   ├── IStatusGrain.cs
│   │   └── StatusGrain.cs
│   ├── Program.cs
│   └── Dockerfile
├── AzureContainerApps.Exercise302.Api/            # Orleans client + REST endpoint
│   ├── Program.cs
│   └── Dockerfile
├── AzureContainerApps.Exercise302.Worker/         # Load generator
│   ├── Program.cs
│   └── Dockerfile
├── AzureContainerApps.Exercise302.AppHost/        # Aspire AppHost (local dev only)
│   └── AppHost.cs
├── AzureContainerApps.Exercise302.ServiceDefaults/ # Shared Aspire service defaults
│   └── Extensions.cs
├── deployment/
│   ├── main.bicep
│   └── roleAssignment.bicep
├── docker-compose.yml
└── README.md
```

## Key Concepts

### Service Connection env-var convention

Azure Container Apps **Service Connections** (via the Azure Portal or `Microsoft.ServiceLinker/linkers`) automatically inject environment variables into container apps when you connect them to Azure services. For Azure Table Storage with a user-assigned managed identity, the injected variables are:

| Variable | Value |
|----------|-------|
| `AZURE_STORAGETABLE_RESOURCEENDPOINT` | `https://<account>.table.core.windows.net/` |
| `AZURE_STORAGETABLE_CLIENTID` | Client ID of the user-assigned managed identity |

This exercise mirrors that convention manually in Bicep so that the same code works both with and without the Service Connector UI.

### Passwordless clustering code

The Orleans Host and Api use these env vars to build a `TableServiceClient` with no credentials in the code:

```csharp
var tableEndpoint = builder.Configuration["AZURE_STORAGETABLE_RESOURCEENDPOINT"]
    ?? throw new InvalidOperationException("AZURE_STORAGETABLE_RESOURCEENDPOINT is not set.");
var clientId = builder.Configuration["AZURE_STORAGETABLE_CLIENTID"]
    ?? throw new InvalidOperationException("AZURE_STORAGETABLE_CLIENTID is not set.");

var credential = new DefaultAzureCredential(
    new DefaultAzureCredentialOptions { ManagedIdentityClientId = clientId });

silo.UseAzureStorageClustering(options =>
    options.TableServiceClient = new TableServiceClient(new Uri(tableEndpoint), credential));
```

`DefaultAzureCredential` automatically selects the right auth source:
- **ACA** → user-assigned managed identity (using `ManagedIdentityClientId`)
- **Local (Azure CLI)** → your logged-in `az` credentials
- **CI/CD** → workload identity or service principal via env vars

### Clustering strategy

The Orleans Host and the Api use a **compile-time** flag (`#if ASPIRE`) to choose their clustering mode:

- **`ASPIRE` defined** (built with `UseAspire=true`, the default) → `UseLocalhostClustering()` — all processes run on the same machine under Aspire.
- **`ASPIRE` not defined** (built with `/p:UseAspire=false`, used by Docker and Azure builds) → `UseAzureStorageClustering()` using `DefaultAzureCredential`.

The Dockerfiles pass `/p:UseAspire=false` to `dotnet publish` so the container images always use Azure Storage clustering.

### RBAC: Storage Table Data Contributor

The managed identity needs the **Storage Table Data Contributor** role on the storage account (role ID `0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3`). This role grants the following permissions on Table Storage:

- `Microsoft.Storage/storageAccounts/tableServices/tables/read`
- `Microsoft.Storage/storageAccounts/tableServices/tables/write`
- `Microsoft.Storage/storageAccounts/tableServices/tables/delete`

> ℹ️ The storage account also sets `allowSharedKeyAccess: false` to enforce key-free access.

### Orleans Dashboard

```csharp
siloBuilder.AddDashboard();
app.MapOrleansDashboard(routePrefix: "/dashboard");
```

```
https://<orleans-host-fqdn>/dashboard
```

## Running Locally with Aspire

> Prerequisites: .NET Aspire workload installed (`dotnet workload install aspire`)

```bash
cd AzureContainerApps.Exercise302.AppHost
dotnet run
```

Aspire starts all three services using `UseLocalhostClustering` — no Azure Storage needed for local dev.

## Running with Docker Compose

Docker Compose uses **Azurite** as the backing store and `DefaultAzureCredential` with service principal credentials (set the `AZURE_*` env vars in `docker-compose.yml` or via a `.env` file).

> **Note:** Azurite supports `DefaultAzureCredential` when the service principal has the right roles on the emulator. Alternatively, set `AZURE_STORAGETABLE_RESOURCEENDPOINT` to a real Azure Storage table endpoint and provide service principal credentials.

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running
- Azure service principal or `az login` credentials

### 1. Configure credentials

Create a `.env` file in the `exercise-302` folder (gitignored):

```env
AZURE_TENANT_ID=<your-tenant-id>
AZURE_CLIENT_ID=<your-service-principal-client-id>
AZURE_CLIENT_SECRET=<your-service-principal-client-secret>
AZURE_STORAGETABLE_RESOURCEENDPOINT=https://<your-storage-account>.table.core.windows.net/
AZURE_STORAGETABLE_CLIENTID=<your-client-id>
```

### 2. Build all images

```bash
docker compose build
```

### 3. Start all containers

```bash
docker compose up
```

| Endpoint | URL |
|----------|-----|
| Orleans Dashboard | http://localhost:8080/dashboard |
| Api `/status` | http://localhost:8081/status |

### 4. Stop the containers

```bash
docker compose down
```

---

## Running manually with Docker

### 1. Create a shared network

```bash
docker network create exercise-302
```

### 2. Build and run the Orleans Host

```bash
docker build -t azure-container-apps-exercise-302-orleans-host:latest \
  ./AzureContainerApps.Exercise302.Orleans.Host

docker run -d --name orleans-host --network exercise-302 -p 8080:8080 \
  -e AZURE_STORAGETABLE_RESOURCEENDPOINT="https://<your-storage-account>.table.core.windows.net/" \
  -e AZURE_STORAGETABLE_CLIENTID="<your-managed-identity-client-id>" \
  -e AZURE_TENANT_ID="<your-tenant-id>" \
  -e AZURE_CLIENT_ID="<your-service-principal-client-id>" \
  -e AZURE_CLIENT_SECRET="<your-service-principal-client-secret>" \
  azure-container-apps-exercise-302-orleans-host:latest
```

### 3. Build and run the Api

> The Api Dockerfile requires the Orleans Host project files (shared grain interfaces), so the build context is the `exercise-302` folder.

```bash
docker build -t azure-container-apps-exercise-302-api:latest \
  -f AzureContainerApps.Exercise302.Api/Dockerfile .

docker run -d --name api --network exercise-302 -p 8081:8080 \
  -e AZURE_STORAGETABLE_RESOURCEENDPOINT="https://<your-storage-account>.table.core.windows.net/" \
  -e AZURE_STORAGETABLE_CLIENTID="<your-managed-identity-client-id>" \
  -e AZURE_TENANT_ID="<your-tenant-id>" \
  -e AZURE_CLIENT_ID="<your-service-principal-client-id>" \
  -e AZURE_CLIENT_SECRET="<your-service-principal-client-secret>" \
  azure-container-apps-exercise-302-api:latest
```

### 4. Build and run the Worker

```bash
docker build -t azure-container-apps-exercise-302-worker:latest \
  ./AzureContainerApps.Exercise302.Worker

docker run --rm --network exercise-302 \
  -e API_BASE_URL=http://api:8080 \
  azure-container-apps-exercise-302-worker:latest
```

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

Run these commands from the `exercise-302` folder:

```bash
REGISTRY=<your-registry-name>

# Orleans Host
az acr build \
  --registry $REGISTRY \
  --image azure-container-apps-exercise-302-orleans-host:latest \
  --file AzureContainerApps.Exercise302.Orleans.Host/Dockerfile \
  ./AzureContainerApps.Exercise302.Orleans.Host

# Api (build context = exercise-302 folder due to shared grain interfaces)
az acr build \
  --registry $REGISTRY \
  --image azure-container-apps-exercise-302-api:latest \
  --file AzureContainerApps.Exercise302.Api/Dockerfile \
  .

# Worker
az acr build \
  --registry $REGISTRY \
  --image azure-container-apps-exercise-302-worker:latest \
  --file AzureContainerApps.Exercise302.Worker/Dockerfile \
  ./AzureContainerApps.Exercise302.Worker
```

### 2. Run the Bicep deployment

```bash
az deployment group create \
  --resource-group <your-resource-group> \
  --template-file deployment/main.bicep \
  --parameters \
      orleansHostAppName=exercise-302-host \
      apiAppName=exercise-302-api \
      workerAppName=exercise-302-worker \
      containerAppEnvironmentName=<env-name> \
      containerRegistryName=$REGISTRY \
      managedIdentityName=exercise-302-identity \
      storageAccountName=<globally-unique-storage-name> \
      orleansHostContainerImage=$REGISTRY.azurecr.io/azure-container-apps-exercise-302-orleans-host:latest \
      apiContainerImage=$REGISTRY.azurecr.io/azure-container-apps-exercise-302-api:latest \
      workerContainerImage=$REGISTRY.azurecr.io/azure-container-apps-exercise-302-worker:latest
```

| Parameter | Description |
|-----------|-------------|
| `orleansHostAppName` | Name for the Orleans Host Container App |
| `apiAppName` | Name for the API Container App |
| `workerAppName` | Name for the Worker Container App |
| `containerAppEnvironmentName` | Name for the Container App Environment (created if it doesn't exist) |
| `containerRegistryName` | Azure Container Registry name (without `.azurecr.io`) |
| `managedIdentityName` | Name of the user-assigned managed identity |
| `storageAccountName` | Name of the **existing** Azure Storage account used for Orleans Table Storage clustering |
| `orleansHostContainerImage` | Fully qualified Orleans Host image tag |
| `apiContainerImage` | Fully qualified API image tag |
| `workerContainerImage` | Fully qualified Worker image tag |
| `location` | Azure region (defaults to resource group location) |

### What the Bicep creates

- **User-assigned managed identity** with `AcrPull` role on the registry
- **Storage Table Data Contributor** role assignment on the existing storage account for the managed identity
- **Container App Environment** (Consumption workload profile)
- **Orleans Host** — external ingress, fixed **2 replicas**; receives `AZURE_STORAGETABLE_RESOURCEENDPOINT` and `AZURE_STORAGETABLE_CLIENTID` as plain env vars
- **Api** — internal ingress, scales **0 → 10** replicas; same storage env vars
- **Worker** — no ingress, fixed **1 replica**, receives `API_BASE_URL` pointing to the Api

> No secrets are stored — the storage endpoint and managed identity client ID are plain (non-sensitive) environment variables. Access is controlled entirely by RBAC.

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
