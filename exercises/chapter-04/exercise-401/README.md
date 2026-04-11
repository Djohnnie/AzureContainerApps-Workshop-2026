[← Back to Exercises](../../README.md) · [↑ View Slide](../../../content/chapter-04/README.md)

# Exercise 401 — KEDA Custom External Scaler for Orleans

This exercise extends [Exercise 301](../../chapter-03/exercise-301/README.md) by adding a custom [KEDA external scaler](https://keda.sh/docs/2.15/concepts/external-scalers/) that drives auto-scaling of the Orleans Host based on the actual grain load in the cluster.

| Service | Role |
|---------|------|
| **Orleans Host** | Silo that hosts Orleans grains; scaled by the custom KEDA scaler |
| **Api** | Minimal API acting as an Orleans client — calls a `StatusGrain` |
| **Worker** | Load generator that hammers the Api to generate grain activity |
| **Orleans Scaler** | gRPC service implementing the KEDA external scaler protocol; inspects the Orleans cluster and reports a metric to KEDA |

## What you''ll learn

- How to write a custom KEDA external scaler in C# using gRPC
- How to expose the KEDA scaler interface (`ExternalScaler` gRPC service) via an ASP.NET Core app
- Configuring HTTP/2 (gRPC) ingress on Azure Container Apps
- Wiring a custom external scaler to an Azure Container Apps scale rule

## Solution Structure

```
exercise-401/
├── AzureContainerApps.Exercise401.Orleans.Host/   # Silo + Orleans Dashboard
├── AzureContainerApps.Exercise401.Api/            # Orleans client + REST endpoint
├── AzureContainerApps.Exercise401.Worker/         # Load generator
├── AzureContainerApps.Exercise401.Orleans.Scaler/ # KEDA custom external scaler (gRPC)
│   ├── Protos/
│   │   └── externalscaler.proto                   # KEDA external scaler proto contract
│   ├── Services/
│   │   └── ExternalScalerService.cs               # gRPC service implementation
│   ├── Program.cs
│   └── Dockerfile
├── AzureContainerApps.Exercise401.AppHost/        # Aspire AppHost (local dev only)
├── AzureContainerApps.Exercise401.ServiceDefaults/
├── deployment/
│   ├── main.bicep
│   └── roleAssignment.bicep
├── docker-compose.yml
└── README.md
```

## Key Concepts

### KEDA External Scaler Protocol

KEDA calls into the scaler via four gRPC methods defined in `externalscaler.proto`:

| Method | Purpose |
|--------|---------|
| `IsActive` | Returns `true` when scaling out is needed (grain load >= upperbound per silo) |
| `StreamIsActive` | Server-streaming variant; called once, pushes `true` whenever load is high |
| `GetMetricSpec` | Advertises the metric name and target size (1) to KEDA |
| `GetMetrics` | Returns the current metric value; KEDA uses this to compute the desired replica count |

### Scaling Logic

The scaler connects to the Orleans cluster as a client and queries `IManagementGrain` for:

- **Total active grain count** across all silos
- **Number of active silos**

It calculates `grainsPerSilo = grainCount / siloCount` and compares against `upperbound` (configured in the KEDA scale rule metadata):

- `grainsPerSilo < upperbound` → scale down (metric = `grainCount / upperbound`, min 1)
- `grainsPerSilo >= upperbound` → scale up (metric = `siloCount + 1`)

The Orleans Host is configured with `minReplicas: 1` and `maxReplicas: 4` and the scale rule references the scaler by its FQDN.

### HTTP/2 Ingress for gRPC

The scaler uses HTTP/2 (`transport: ''http2''`) on the Container Apps ingress so that KEDA can communicate with it via gRPC. Normal HTTP/1.1 ingress would silently break the gRPC streaming methods.

## Running Locally with Aspire

> The scaler is **not part of the Aspire AppHost** — it requires a running Orleans cluster via Azure Storage clustering and is only used in deployed environments.

```bash
cd AzureContainerApps.Exercise401.AppHost
dotnet run
```

## Running with Docker Compose

```bash
docker compose build
docker compose up
```

| Endpoint | URL |
|----------|-----|
| Orleans Dashboard | http://localhost:8080/dashboard |
| Api `/status` | http://localhost:8081/status |
| Scaler (gRPC) | http://localhost:8082 |

```bash
docker compose down
```

---

## Deploying to Azure Container Apps

### Prerequisites

- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) installed and logged in (`az login`)
- An existing Azure resource group and Azure Container Registry

### 1. Push all images to your registry

Run from the `exercise-401` folder:

```bash
REGISTRY=<your-registry-name>

# Orleans Host
az acr build \
  --registry $REGISTRY \
  --image azure-container-apps-exercise-401-orleans-host:latest \
  --file AzureContainerApps.Exercise401.Orleans.Host/Dockerfile \
  ./AzureContainerApps.Exercise401.Orleans.Host

# Api
az acr build \
  --registry $REGISTRY \
  --image azure-container-apps-exercise-401-api:latest \
  --file AzureContainerApps.Exercise401.Api/Dockerfile \
  .

# Worker
az acr build \
  --registry $REGISTRY \
  --image azure-container-apps-exercise-401-worker:latest \
  --file AzureContainerApps.Exercise401.Worker/Dockerfile \
  ./AzureContainerApps.Exercise401.Worker

# Orleans Scaler
az acr build \
  --registry $REGISTRY \
  --image azure-container-apps-exercise-401-orleans-scaler:latest \
  --file AzureContainerApps.Exercise401.Orleans.Scaler/Dockerfile \
  ./AzureContainerApps.Exercise401.Orleans.Scaler
```

### 2. Run the Bicep deployment

```bash
az deployment group create \
  --resource-group <your-resource-group> \
  --template-file deployment/main.bicep \
  --parameters \
      orleansHostAppName=exercise-401-host \
      apiAppName=exercise-401-api \
      workerAppName=exercise-401-worker \
      scalerAppName=exercise-401-scaler \
      containerAppEnvironmentName=<env-name> \
      containerRegistryName=$REGISTRY \
      managedIdentityName=exercise-401-identity \
      storageConnectionString="<your-storage-connection-string>" \
      orleansHostContainerImage=$REGISTRY.azurecr.io/azure-container-apps-exercise-401-orleans-host:latest \
      apiContainerImage=$REGISTRY.azurecr.io/azure-container-apps-exercise-401-api:latest \
      workerContainerImage=$REGISTRY.azurecr.io/azure-container-apps-exercise-401-worker:latest \
      scalerContainerImage=$REGISTRY.azurecr.io/azure-container-apps-exercise-401-orleans-scaler:latest
```

| Parameter | Description |
|-----------|-------------|
| `orleansHostAppName` | Name for the Orleans Host Container App |
| `apiAppName` | Name for the API Container App |
| `workerAppName` | Name for the Worker Container App |
| `scalerAppName` | Name for the Orleans Scaler Container App |
| `containerAppEnvironmentName` | Name for the Container App Environment |
| `containerRegistryName` | Azure Container Registry name (without `.azurecr.io`) |
| `managedIdentityName` | Name of the user-assigned managed identity |
| `storageConnectionString` | Azure Storage connection string for Orleans clustering |
| `*ContainerImage` | Fully qualified image tags |

### What the Bicep creates

| Container App | Ingress | Scale |
|--------------|---------|-------|
| Orleans Scaler | **External** — HTTP/2 (gRPC), port 8080 | fixed **1 replica** |
| Orleans Host | **External** — HTTP/1.1, serves `/dashboard` | **1–4** replicas via custom external scaler |
| API | **Internal** | **0–10** replicas via HTTP concurrency |
| Worker | **None** | fixed **1 replica** |

The Orleans Host scale rule (`orleans-scaler`) points to the scaler''s FQDN and passes `upperbound: 4` as metadata. KEDA calls the scaler gRPC service every polling interval to get the current metric value and adjusts the replica count accordingly.

### 3. Retrieve deployment outputs

```bash
az deployment group show \
  --resource-group <your-resource-group> \
  --name main \
  --query properties.outputs \
  --output json
```

Outputs:

| Output | Value |
|--------|-------|
| `scalerUrl` | HTTPS URL of the scaler gRPC endpoint |
| `orleansHostUrl` | HTTPS URL of the Orleans Dashboard |
| `apiInternalFqdn` | Internal FQDN of the API (for intra-environment routing) |

---

## Scaling Behaviour

The Worker generates continuous load by calling the API. Each API call activates a `StatusGrain` (which deactivates on idle after 5 seconds). As grain activations accumulate across silos, the scaler detects when grains-per-silo exceeds `upperbound` (4) and signals KEDA to add another silo replica — up to the maximum of 4.

Monitor scaling in the Azure portal under **Container Apps → Orleans Host → Revisions and replicas**, or watch the scaler logs for metric values.