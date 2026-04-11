[← Back to Exercises](../../README.md) · [↑ View Slide](../../../content/chapter-02/README.md)

# Exercise 203

## Auto-Scaling a Container App from Zero

Deploy a minimal API that scales from zero to ten replicas based on HTTP load, and a worker service that generates that load automatically. Watch the API scale out as the worker fires bulk requests, then scale back to zero when the worker stops.

---

> ### 💬 Copilot Prompt
>
> ```
> Create two .NET 10 projects:
> - AzureContainerApps.Exercise203.Api: a minimal ASP.NET API with a single
>   GET /status endpoint that returns the hostname of the server as JSON.
> - AzureContainerApps.Exercise203.Worker: a .NET Worker Service that reads
>   API_BASE_URL from an environment variable and continuously fires 50
>   concurrent HTTP requests per second to the /status endpoint, logging
>   each server name it receives.
> Create a Dockerfile for each project and a docker-compose.yml that runs
> both services, setting API_BASE_URL=http://api:8080 on the worker.
> ```

---

## Projects

| Project | Description |
|---------|-------------|
| `AzureContainerApps.Exercise203.Api` | ASP.NET minimal API — exposes `GET /status` returning the container hostname as `{ "server": "<name>" }` |
| `AzureContainerApps.Exercise203.Worker` | .NET Worker Service — reads `API_BASE_URL` and fires 50 concurrent requests per second to `/status`, logging the server name from each response |

---

## Running with Docker Compose

Docker Compose builds and wires both containers. The worker waits for the API to be healthy before starting to generate load.

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running

### 1. Build both images

Open a terminal in the `exercise-203` folder and run:

```bash
docker compose build
```

### 2. Start both containers

```bash
docker compose up
```

The worker starts firing requests immediately. Watch the logs — you will see the server name in every response (the same container ID, since only one replica runs locally).

### 3. Stop the containers

```bash
docker compose down
```

---

## Running manually with Docker

### 1. Create a shared network

```bash
docker network create exercise-203
```

### 2. Build and run the API

```bash
docker build -t azure-container-apps-exercise-203-api:latest ./AzureContainerApps.Exercise203.Api
docker run --rm --name api --network exercise-203 azure-container-apps-exercise-203-api:latest
```

### 3. Build and run the Worker

```bash
docker build -t azure-container-apps-exercise-203-worker:latest ./AzureContainerApps.Exercise203.Worker
docker run --rm --network exercise-203 \
  -e API_BASE_URL=http://api:8080 \
  azure-container-apps-exercise-203-worker:latest
```

| Flag / Variable | Description |
|----------------|-------------|
| `--network exercise-203` | Connects both containers on the same Docker network |
| `-e API_BASE_URL=http://api:8080` | Points the worker at the API container by name |

---

## Deploying to Azure Container Apps

The `deployment/main.bicep` file deploys both containers as Azure Container Apps in a shared environment:

| Container App | Ingress | Scale |
|--------------|---------|-------|
| API (`apiAppName`) | **Internal** — reachable only within the environment | min **0**, max **10** |
| Worker (`workerAppName`) | **None** — outbound only | always **1** |

The API is configured with an **HTTP scaling rule** (`concurrentRequests: 10`): one additional replica is added for every 10 concurrent in-flight requests. Because the worker fires 50 concurrent requests per second, the API scales up to roughly 5 replicas within seconds. When the worker is stopped, the API drains and scales back down to zero.

### Prerequisites

- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) installed
- Logged in to Azure (`az login`)
- An existing Azure resource group and Azure Container Registry

### 1. Push both images to your registry

```bash
az acr build --registry <your-registry> --image azure-container-apps-exercise-203-api:latest ./AzureContainerApps.Exercise203.Api
az acr build --registry <your-registry> --image azure-container-apps-exercise-203-worker:latest ./AzureContainerApps.Exercise203.Worker
```

### 2. Run the Bicep deployment

Open a terminal in the `exercise-203` folder and run:

```bash
az deployment group create \
  --resource-group <your-resource-group> \
  --template-file deployment/main.bicep \
  --parameters apiAppName=<api-app-name> \
               workerAppName=<worker-app-name> \
               managedIdentityName=<identity-name> \
               containerAppEnvironmentName=<env-name> \
               containerRegistryName=<registry-name> \
               apiContainerImage=<your-registry>.azurecr.io/azure-container-apps-exercise-203-api:latest \
               workerContainerImage=<your-registry>.azurecr.io/azure-container-apps-exercise-203-worker:latest
```

| Parameter | Description |
|-----------|-------------|
| `apiAppName` | Name for the API Container App (internal ingress) |
| `workerAppName` | Name for the Worker Container App (no ingress) |
| `managedIdentityName` | Name for the shared user-assigned managed identity |
| `containerAppEnvironmentName` | Name for the Container App Environment (created if it does not exist) |
| `containerRegistryName` | Name of the Azure Container Registry (without `.azurecr.io`) |
| `apiContainerImage` | Fully qualified image name for the API |
| `workerContainerImage` | Fully qualified image name for the Worker |

### 3. Observe scale-out in the Azure Portal

Once deployed, open the API Container App in the Azure Portal and navigate to **Scale** → **Revisions and replicas**. Within a few seconds of the worker starting, the replica count will climb from 0 toward 10 as the HTTP load builds up.

To see scale-to-zero, stop the Worker Container App. The API will drain all active requests and, after a cooldown period, return to 0 replicas.

---

## How the Scaling Works

Azure Container Apps uses [KEDA](https://keda.sh/) under the hood to evaluate scaling rules. The `http` rule type monitors the number of concurrent HTTP requests currently being processed by the Container App. When the concurrent request count exceeds `concurrentRequests * currentReplicas`, ACA schedules an additional replica.

| Concurrent requests | Expected replicas |
|--------------------|------------------|
| 0 | 0 (scale to zero) |
| 1–10 | 1 |
| 11–20 | 2 |
| 41–50 | 5 |
| 91–100 | 10 (maximum) |

The worker fires **50 concurrent requests per second**, so the steady-state replica count is typically around 5. Values may vary slightly due to timing and request latency.
