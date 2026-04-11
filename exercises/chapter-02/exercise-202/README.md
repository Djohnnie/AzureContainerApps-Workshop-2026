[← Back to Exercises](../../README.md)

# Exercise 202

## Multi-Revision Container App with Traffic Splitting

Take the Exercise 102 web page and add a `BACKGROUND_COLOR` environment variable that controls the page's background. Deploy two revisions to a **multi-revision Azure Container App** — one with a dark-blue background and one with a dark-green background — and split traffic between them at **25% / 75%**.

---

> ### 💬 Copilot Prompt
>
> ```
> Copy the AzureContainerApps.Exercise102 web app into a new project called
> AzureContainerApps.Exercise202. Add a BACKGROUND_COLOR environment variable
> that sets the CSS background-color of the page body. Default to #1e1e2e
> when the variable is not set. Display the current color value in a badge
> on the page.
> ```

---

## Running with Docker Desktop

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running

### 1. Build the image

Open a terminal in the `AzureContainerApps.Exercise202` project folder and run:

```bash
docker build -t azure-container-apps-exercise-202:latest .
```

### 2. Run the darkblue variant

```bash
docker run --rm -p 8080:8080 \
  -e BACKGROUND_COLOR=darkblue \
  azure-container-apps-exercise-202:latest
```

Open [http://localhost:8080](http://localhost:8080) — the page renders with a dark-blue background.

### 3. Run the darkgreen variant

Stop the previous container, then:

```bash
docker run --rm -p 8080:8080 \
  -e BACKGROUND_COLOR=darkgreen \
  azure-container-apps-exercise-202:latest
```

Refresh your browser — the page now has a dark-green background. Same image, different runtime environment variable.

### 4. Verify in Docker Desktop

```bash
docker images azure-container-apps-exercise-202
```

---

## Running with Docker Compose

The included `docker-compose.yml` pre-configures the `darkblue` variant:

```bash
docker compose up --build
```

Open [http://localhost:8080](http://localhost:8080). To switch to darkgreen, edit the `BACKGROUND_COLOR` value in `docker-compose.yml` and run `docker compose up --build` again.

---

## Deploying to Azure Container Apps

The `deployment/main.bicep` deploys the image as a **multi-revision Container App** with two named revisions and a weighted traffic split:

| Revision | Background | Traffic weight |
|----------|-----------|---------------|
| `<app>--blue` | `darkblue` | **25%** |
| `<app>--green` | `darkgreen` | **75%** |

### How multi-revision deployment works

Bicep deploys the Container App **twice in sequence**:

1. **First module** — creates the Container App with `revisionSuffix: 'blue'` and `BACKGROUND_COLOR=darkblue`. All traffic goes to the blue revision.
2. **Second module** — updates the same Container App with `revisionSuffix: 'green'` and `BACKGROUND_COLOR=darkgreen`. Traffic is redistributed to 25% blue / 75% green.

Azure Container Apps keeps both revisions alive. Each request is independently routed according to the weights, so if you refresh the page many times, roughly three out of four requests will show the green background.

### Prerequisites

- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) installed
- Logged in to Azure (`az login`)
- An existing Azure resource group and Azure Container Registry

### 1. Push the image to a registry

```bash
az acr build --registry <your-registry> --image azure-container-apps-exercise-202:latest .
```

### 2. Run the Bicep deployment

Open a terminal in the `exercise-202` folder and run:

```bash
az deployment group create \
  --resource-group <your-resource-group> \
  --template-file deployment/main.bicep \
  --parameters containerAppName=<app-name> \
               managedIdentityName=<identity-name> \
               containerAppEnvironmentName=<env-name> \
               containerRegistryName=<registry-name> \
               containerImage=<your-registry>.azurecr.io/azure-container-apps-exercise-202:latest
```

| Parameter | Description |
|-----------|-------------|
| `--resource-group` | Name of the existing Azure resource group |
| `containerAppName` | Name to give the new Container App |
| `managedIdentityName` | Name for the user-assigned managed identity (e.g. `aca-exercise-202-identity`) |
| `containerAppEnvironmentName` | Name for the Container App Environment (created if it does not exist) |
| `containerRegistryName` | Name of the existing Azure Container Registry (without `.azurecr.io`) |
| `containerImage` | Fully qualified container image reference |

The Bicep file:
- Creates a **user-assigned managed identity** and grants it the `AcrPull` role on the registry
- Creates the Container App Environment (Consumption workload profile) if it does not exist
- Deploys two revisions of the Container App with `activeRevisionsMode: Multiple`
- Sets `BACKGROUND_COLOR=darkblue` on the first revision and `BACKGROUND_COLOR=darkgreen` on the second
- Applies a **25% / 75% traffic split** between the two revisions

### 3. Observe the traffic split

Once the deployment completes, the FQDN is printed as an output:

```
https://<containerAppFqdn>
```

Open it in your browser and refresh several times. You will see the page alternate between a dark-blue and a dark-green background, with the green background appearing roughly three times as often.

> **Tip:** To verify each revision independently, open the Container App in the Azure Portal, go to **Revisions and replicas**, and click the direct URL for each revision.

### 4. Check active revisions in the Azure Portal

Navigate to your Container App in the Azure Portal and open **Revisions and replicas**. You will see:

| Revision name | Status | Traffic |
|---------------|--------|---------|
| `<app>--blue` | Running | 25% |
| `<app>--green` | Running | 75% |

---

## Deployment Structure

```
deployment/
├── main.bicep                          # Orchestrates shared resources + two revision modules
└── modules/
    └── container-app-revision.bicep    # Reusable module: deploys one revision with a given
                                        # background color and traffic weights
```

### Shared resources (in `main.bicep`)

| Resource | Purpose |
|----------|---------|
| User-assigned managed identity | Authenticates the Container App to pull from ACR |
| `AcrPull` role assignment | Grants the identity read access to the registry |
| Container App Environment | Consumption workload profile; created if it does not exist |

### Per-revision module parameters

| Parameter | Blue revision | Green revision |
|-----------|--------------|---------------|
| `revisionSuffix` | `blue` | `green` |
| `backgroundColor` | `darkblue` | `darkgreen` |
| `trafficWeights` | `[{blue: 100%}]` | `[{blue: 25%}, {green: 75%}]` |
