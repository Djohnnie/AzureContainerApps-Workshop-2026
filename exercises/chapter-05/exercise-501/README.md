[← Back to Exercises](../../README.md)

# Exercise 501 — Azure Container Jobs: AI-Powered Quote of the Day

This exercise introduces **Azure Container Jobs** — a feature of Azure Container Apps that runs containerised workloads on-demand, on a schedule, or in response to events. You will deploy a scheduled job that calls **Azure OpenAI** through the **Microsoft Semantic Kernel Agent Framework** to generate a fresh inspirational quote every minute and store it in **Azure Blob Storage**. A **Blazor WebAssembly** frontend hosted as a Container App displays the latest quote to the user.

| Component | Role |
|-----------|------|
| **Web** (Blazor WebAssembly) | Container App — serves the frontend and exposes `/api/quote` backed by Blob Storage |
| **QuoteJob** (.NET Console App) | Container Job — uses a Semantic Kernel `ChatCompletionAgent` + Azure OpenAI to write a new `quote.json` to blob storage every minute |

## What you'll learn

- How to create and configure an **Azure Container Job** with a cron schedule
- The difference between Container Apps (long-running) and Container Jobs (run-to-completion)
- How to use the **Microsoft Semantic Kernel Agent Framework** with Azure OpenAI in .NET
- How to read and write blobs with **Azure Blob Storage SDK**
- How to connect a Blazor WASM frontend to a server-side API that reads from blob storage

## Solution Structure

```
exercise-501/
├── AzureContainerApps.Exercise501.Web/             # Blazor Web App (ASP.NET Core host + Blazor WASM)
│   ├── Components/
│   │   ├── App.razor
│   │   ├── Routes.razor
│   │   ├── Layout/MainLayout.razor
│   │   └── Pages/Error.razor, NotFound.razor
│   ├── Program.cs                                  # /api/quote minimal API + Blob Storage
│   └── Dockerfile
├── AzureContainerApps.Exercise501.Web.Client/      # Blazor WASM client project
│   └── Components/Pages/Home.razor                 # Quote display page (InteractiveWebAssembly)
├── AzureContainerApps.Exercise501.QuoteJob/        # .NET Console App — Container Job
│   ├── Program.cs                                  # Semantic Kernel agent + Blob Storage write
│   └── Dockerfile
├── deployment/
│   ├── main.bicep                                  # Container App + Container Job (Schedule trigger)
│   └── roleAssignment.bicep
├── docker-compose.yml
└── README.md
```

## Key Concepts

### Azure Container Jobs vs Container Apps

| | Container App | Container Job |
|---|---|---|
| **Lifecycle** | Long-running (always on) | Run-to-completion |
| **Trigger** | HTTP / KEDA scale rules | Schedule (cron), Event, Manual |
| **Billing** | Per active replica | Per execution |
| **Use cases** | APIs, frontends, background services | ETL, AI batch, maintenance tasks |

### Container Job Schedule Trigger

The Bicep resource `Microsoft.App/jobs` uses `triggerType: 'Schedule'` with a `cronExpression`:

```bicep
configuration: {
  triggerType: 'Schedule'
  scheduleTriggerConfig: {
    cronExpression: '*/1 * * * *'  // every minute
    parallelism: 1
    replicaCompletionCount: 1
  }
}
```

### Microsoft Semantic Kernel Agent Framework

The quote job uses a `ChatCompletionAgent` from `Microsoft.SemanticKernel.Agents.Core`:

```csharp
var kernel = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(modelName, endpoint, apiKey)
    .Build();

ChatCompletionAgent agent = new()
{
    Name = "QuoteAgent",
    Instructions = "...",
    Kernel = kernel
};

ChatHistory history = [];
history.AddUserMessage("Generate a unique inspirational quote of the day.");

await foreach (var message in agent.InvokeAsync(history))
{
    // collect the AI response
}
```

### Data Flow

```
[Cron (*/1 * * * *)]
       │
       ▼
[Container Job: QuoteJob]
       │  Azure OpenAI (Semantic Kernel Agent)
       │  → generates quote JSON
       ▼
[Azure Blob Storage]
   container: quotes
   blob: quote.json
       │
       ▼
[Container App: Web]
   GET /api/quote  ──reads──▶  BlobServiceClient
       │
       ▼
[Blazor WASM client]
   Home.razor (InteractiveWebAssembly)
```

## Running Locally with Docker Compose

```bash
docker compose build
docker compose up -d azurite web
```

The frontend is available at **http://localhost:8080**.

Without a quote in blob storage, the app shows a built-in fallback quote. To seed a real AI quote (requires Azure OpenAI credentials), update the `quote-job` service in `docker-compose.yml` with your values and run:

```bash
docker compose run --rm quote-job
```

```bash
docker compose down
```

---

## Deploying to Azure Container Apps

### Prerequisites

- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) installed and logged in (`az login`)
- An Azure Container Registry, resource group, and Azure OpenAI resource

### 1. Build and push images

Run from the `exercise-501` folder:

```bash
REGISTRY=<your-registry-name>

# Frontend Web App
az acr build \
  --registry $REGISTRY \
  --image azure-container-apps-exercise-501-web:latest \
  --file AzureContainerApps.Exercise501.Web/Dockerfile \
  .

# Quote Job
az acr build \
  --registry $REGISTRY \
  --image azure-container-apps-exercise-501-quote-job:latest \
  --file AzureContainerApps.Exercise501.QuoteJob/Dockerfile \
  ./AzureContainerApps.Exercise501.QuoteJob
```

### 2. Deploy with Bicep

```bash
az deployment group create \
  --resource-group <your-resource-group> \
  --template-file deployment/main.bicep \
  --parameters \
      webAppName=exercise-501-web \
      quoteJobName=exercise-501-quote-job \
      containerAppEnvironmentName=<env-name> \
      containerRegistryName=$REGISTRY \
      managedIdentityName=exercise-501-identity \
      blobStorageConnectionString="<your-blob-storage-connection-string>" \
      azureOpenAiEndpoint="<your-azure-openai-endpoint>" \
      azureOpenAiApiKey="<your-azure-openai-api-key>" \
      azureOpenAiModelName="<your-model-deployment-name>" \
      webContainerImage=$REGISTRY.azurecr.io/azure-container-apps-exercise-501-web:latest \
      quoteJobContainerImage=$REGISTRY.azurecr.io/azure-container-apps-exercise-501-quote-job:latest
```

| Parameter | Description |
|-----------|-------------|
| `webAppName` | Name for the frontend Container App |
| `quoteJobName` | Name for the Container Job |
| `containerAppEnvironmentName` | Name of the Container App Environment |
| `containerRegistryName` | Azure Container Registry name (without `.azurecr.io`) |
| `managedIdentityName` | Name of the user-assigned managed identity |
| `blobStorageConnectionString` | Azure Storage connection string (Blob Service) |
| `azureOpenAiEndpoint` | Azure OpenAI resource endpoint URL |
| `azureOpenAiApiKey` | Azure OpenAI API key |
| `azureOpenAiModelName` | Deployment name (e.g. `gpt-4o`) |
| `webContainerImage` | Fully qualified image tag for the web app |
| `quoteJobContainerImage` | Fully qualified image tag for the quote job |

### What the Bicep creates

| Resource | Type | Description |
|----------|------|-------------|
| Container App Environment | `Microsoft.App/managedEnvironments` | Shared environment |
| Web Container App | `Microsoft.App/containerApps` | External ingress, 1–5 replicas |
| Quote Container Job | `Microsoft.App/jobs` | Schedule trigger, cron `*/1 * * * *` |
| Managed Identity | `Microsoft.ManagedIdentity/userAssignedIdentities` | ACR pull access |

### 3. Retrieve the web app URL

```bash
az deployment group show \
  --resource-group <your-resource-group> \
  --name main \
  --query properties.outputs.webAppUrl.value \
  --output tsv
```

### 4. Monitor the Container Job

View job execution history in the Azure Portal under **Container App Job → Execution history**, or via CLI:

```bash
az containerapp job execution list \
  --name exercise-501-quote-job \
  --resource-group <your-resource-group> \
  --output table
```

Trigger a manual execution:

```bash
az containerapp job start \
  --name exercise-501-quote-job \
  --resource-group <your-resource-group>
```

---

## Environment Variables

### Web App

| Variable | Description |
|----------|-------------|
| `BLOB_STORAGE_CONNECTION_STRING` | Azure Blob Storage connection string; used to read `quotes/quote.json` |

### Quote Job

| Variable | Description |
|----------|-------------|
| `BLOB_STORAGE_CONNECTION_STRING` | Azure Blob Storage connection string; used to write `quotes/quote.json` |
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI resource endpoint URL |
| `AZURE_OPENAI_API_KEY` | Azure OpenAI API key |
| `AZURE_OPENAI_MODEL_NAME` | Model deployment name (e.g. `gpt-4o`, `gpt-4o-mini`) |
