[← Back to Exercises](../../README.md) · [↑ View Slide](../../../content/chapter-02/README.md)

# Exercise 201

## Multi-Container App with internal API

Connect two containers — a web front end and a minimal API back end — using an environment variable to wire them together. The web app displays a Hello World ASCII art page and shows the hostname of the API container it is talking to.

---

> ### 💬 Copilot Prompt
>
> ```
> Using AzureContainerApps.Exercise102 as a base, create two .NET 10 projects:
> - AzureContainerApps.Exercise201.Web: a minimal ASP.NET web app that reads an
>   environment variable API_BASE_URL and calls GET /server-name on that API,
>   displaying the result alongside the Hello World ASCII art page.
> - AzureContainerApps.Exercise201.Api: a minimal ASP.NET API with a single
>   GET /server-name endpoint that returns the hostname of the server.
> Create a Dockerfile for each project and a docker-compose.yml that runs both
> services, setting API_BASE_URL=http://api:8080 on the web service.
> ```

---

## Projects

| Project | Description |
|---------|-------------|
| `AzureContainerApps.Exercise201.Web` | ASP.NET minimal web app — renders Hello World ASCII art and fetches the API server name |
| `AzureContainerApps.Exercise201.Api` | ASP.NET minimal API — exposes `GET /server-name` returning the container hostname |

---

## Running with Docker Compose

Docker Compose builds and wires both containers together, setting the `API_BASE_URL` environment variable automatically so the web app can reach the API over the internal Docker network.

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running

### 1. Build both images

Open a terminal in the `exercise-201` folder and run:

```bash
docker compose build
```

### 2. Start both containers

```bash
docker compose up
```

Open [http://localhost:8080](http://localhost:8080) in your browser. The page shows the Hello World ASCII art and the hostname of the running API container.

### 3. Stop the containers

```bash
docker compose down
```

---

## Running manually with Docker Desktop

### 1. Create a shared network

```bash
docker network create exercise-201
```

### 2. Build and run the API

```bash
docker build -t azure-container-apps-exercise-201-api:latest ./AzureContainerApps.Exercise201.Api
docker run --rm --name api --network exercise-201 azure-container-apps-exercise-201-api:latest
```

### 3. Build and run the Web app

```bash
docker build -t azure-container-apps-exercise-201-web:latest ./AzureContainerApps.Exercise201.Web
docker run --rm -p 8080:8080 --network exercise-201 \
  -e API_BASE_URL=http://api:8080 \
  azure-container-apps-exercise-201-web:latest
```

Open [http://localhost:8080](http://localhost:8080) in your browser.

| Flag / Variable | Description |
|----------------|-------------|
| `--network exercise-201` | Connects both containers on the same Docker network so they can reach each other by name |
| `-e API_BASE_URL=http://api:8080` | Tells the web app where to find the API — using the container name `api` as the hostname |

---

## Deploying to Azure Container Apps

The `deployment/main.bicep` file deploys both containers as Azure Container Apps in a shared environment. The API uses **internal ingress** (not reachable from the internet), and the web app uses **external ingress** with the API's internal FQDN wired in via `API_BASE_URL`. A single user-assigned managed identity is shared by both apps to pull images from ACR.

### Prerequisites

- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) installed
- Logged in to Azure (`az login`)
- An existing Azure resource group and Azure Container Registry

### 1. Push both images to your registry

```bash
az acr build --registry <your-registry> --image azure-container-apps-exercise-201-api:latest ./AzureContainerApps.Exercise201.Api
az acr build --registry <your-registry> --image azure-container-apps-exercise-201-web:latest ./AzureContainerApps.Exercise201.Web
```

### 2. Run the Bicep deployment

Open a terminal in the `exercise-201` folder and run:

```bash
az deployment group create \
  --resource-group <your-resource-group> \
  --template-file deployment/main.bicep \
  --parameters webAppName=<web-app-name> \
               apiAppName=<api-app-name> \
               containerAppEnvironmentName=<env-name> \
               containerRegistryName=<registry-name> \
               webContainerImage=<your-registry>.azurecr.io/azure-container-apps-exercise-201-web:latest \
               apiContainerImage=<your-registry>.azurecr.io/azure-container-apps-exercise-201-api:latest
```

| Parameter | Description |
|-----------|-------------|
| `--resource-group` | Name of the existing Azure resource group to deploy into |
| `webAppName` | Name to give the Web Container App (external ingress) |
| `apiAppName` | Name to give the API Container App (internal ingress only) |
| `containerAppEnvironmentName` | Name for the shared Container App Environment (created if it does not exist) |
| `containerRegistryName` | Name of the Azure Container Registry (without `.azurecr.io`) |
| `webContainerImage` | Fully qualified image name for the web app (e.g. `myregistry.azurecr.io/azure-container-apps-exercise-201-web:latest`) |
| `apiContainerImage` | Fully qualified image name for the API (e.g. `myregistry.azurecr.io/azure-container-apps-exercise-201-api:latest`) |

### 3. Open the deployed web app

Once the deployment completes, the web app FQDN is printed as an output:

```
https://<webAppFqdn>
```

The API is only accessible internally — the web app reaches it via its internal FQDN, which is automatically injected as `API_BASE_URL` by the Bicep template.

---

## Load Balancing with Fixed Replicas

The API container app is configured with a **fixed replica count of 2** (`minReplicas: 2`, `maxReplicas: 2`). No KEDA scalers are used — the replica count is static and does not respond to traffic volume.

With two API replicas running, Azure Container Apps automatically load-balances requests across both instances. Because each replica has a unique hostname, you can observe this behaviour directly: refresh the web page multiple times and you will see the **API server name** alternate between the two replica hostnames.

This illustrates how Azure Container Apps routes traffic across replicas transparently, without any client-side configuration.
