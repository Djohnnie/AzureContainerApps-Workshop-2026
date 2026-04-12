[← Back to Exercises](../../README.md)

# Exercise 502 — Azure Container Jobs: Event-Triggered via Service Bus

This exercise demonstrates **event-driven Azure Container Jobs** using an **Azure Service Bus** queue as the KEDA scaler trigger. The user types a theme in the Blazor frontend, which sends a message to the queue. KEDA detects the message and triggers the Container Job, which calls **Azure OpenAI** via the **Semantic Kernel Agent Framework** to generate a themed quote and store it in **Azure Blob Storage**. The frontend auto-refreshes every 10 seconds to pick up the new quote.

Compare with [Exercise 501](../exercise-501/README.md) which uses a **cron schedule** trigger instead.

| Component | Role |
|-----------|------|
| **Web** (Blazor WebAssembly) | Container App — serves the frontend, exposes `GET /api/quote` and `POST /api/quote/request` |
| **QuoteJob** (.NET Console App) | Container Job — triggered by a Service Bus message; generates a themed quote with Azure OpenAI and writes `quote502.json` to Blob Storage |

## What you'll learn

- How to create an **event-driven Azure Container Job** triggered by a KEDA Service Bus scaler
- The difference between **Schedule** (501) and **Event** (502) Container Job trigger types
- How to send and receive **Azure Service Bus** messages from .NET
- How to build a Blazor WASM frontend that accepts user input and polls for updates
- How to use the **Microsoft Semantic Kernel Agent Framework** with Azure OpenAI

## Solution Structure

```
exercise-502/
├── AzureContainerApps.Exercise502.Web/             # Blazor Web App (ASP.NET Core host + Blazor WASM)
│   ├── Components/
│   │   ├── App.razor
│   │   ├── Routes.razor
│   │   ├── Layout/MainLayout.razor
│   │   └── Pages/Error.razor
│   ├── Program.cs                                  # /api/quote + /api/quote/request minimal APIs
│   └── Dockerfile
├── AzureContainerApps.Exercise502.Web.Client/      # Blazor WASM client project
│   └── Components/Pages/Home.razor                 # Theme input, Generate button, auto-refresh quote
├── AzureContainerApps.Exercise502.QuoteJob/        # .NET Console App — Container Job
│   ├── Program.cs                                  # Reads Service Bus message → AI quote → Blob Storage
│   └── Dockerfile
├── deployment/
│   ├── main.bicep                                  # Container App + Container Job (Event trigger)
│   └── roleAssignment.bicep
├── docker-compose.yml
└── README.md
```

## Key Concepts

### Trigger comparison: Schedule vs Event

| | Exercise 501 | Exercise 502 |
|---|---|---|
| **Trigger type** | Schedule (cron) | Event (KEDA) |
| **Scaler** | `*/1 * * * *` | `azure-servicebus` |
| **How a new quote is requested** | Automatic, every minute | User submits a theme from the frontend |
| **Job starts when** | Timer fires | A message lands on the Service Bus queue |
| **Cost** | Runs every minute regardless | Runs only when needed |

### Azure Container Job Event Trigger (KEDA)

The Bicep resource `Microsoft.App/jobs` uses `triggerType: 'Event'` with a KEDA `azure-servicebus` scale rule:

```bicep
configuration: {
  triggerType: 'Event'
  eventTriggerConfig: {
    replicaCompletionCount: 1
    parallelism: 1
    scale: {
      minExecutions: 0
      maxExecutions: 10
      pollingInterval: 30
      rules: [
        {
          name: 'servicebus-rule'
          type: 'azure-servicebus'
          metadata: {
            queueName: 'quote-requests'
            messageCount: '1'
          }
          auth: [
            {
              secretRef: 'servicebus-connection-string'
              triggerParameter: 'connection'
            }
          ]
        }
      ]
    }
  }
}
```

When the queue depth reaches `messageCount: '1'`, Azure spins up a job execution. With `minExecutions: 0`, no jobs run when the queue is empty.

### Message Flow

```
[User types a theme and clicks Generate]
        │
        ▼
[POST /api/quote/request]
[Web Container App]
        │  sends {"theme": "resilience"} message
        ▼
[Azure Service Bus Queue: quote-requests]
        │  KEDA detects message (pollingInterval: 30s)
        ▼
[Container Job: QuoteJob]
        │  ReceiveMessageAsync → parse theme
        │  Azure OpenAI (Semantic Kernel Agent)
        │  → generates themed quote JSON
        │  CompleteMessageAsync (ack)
        ▼
[Azure Blob Storage]
   container: quotes / blob: quote502.json
        │
        ▼
[GET /api/quote — polled every 10s by Blazor WASM]
```

### At-least-once delivery

The job calls `receiver.CompleteMessageAsync()` **only after** a successful blob upload. If the job crashes before completing, the message becomes available again and a new job execution will retry it.

### Local dev fallback

Azurite (the local Azure Storage emulator) does not support Service Bus. For local development:

- Set `QUOTE_THEME` in the `quote-job` service environment to bypass Service Bus entirely.
- Leave `SERVICE_BUS_CONNECTION_STRING` empty in the `web` service to disable the `POST /api/quote/request` endpoint gracefully.

## Running Locally with Docker Compose

```bash
docker compose build
docker compose up -d azurite web
```

The frontend is available at **http://localhost:5002**.

Without a quote in blob storage, the app shows a built-in fallback quote. To generate a themed AI quote locally (requires Azure OpenAI credentials), update the `AZURE_OPENAI_*` variables in `docker-compose.yml` and run the job with a theme override:

```bash
# Set your Azure OpenAI credentials in docker-compose.yml first, then:
docker compose --profile job run --rm quote-job
```

```bash
docker compose down
```

---

## Deploying to Azure Container Apps

### Prerequisites

- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) installed and logged in (`az login`)
- An Azure Container Registry, resource group, Azure OpenAI resource, Azure Storage account, and Azure Service Bus namespace with a queue named `quote-requests`

### 1. Build and push images

Run from the `exercise-502` folder:

```bash
REGISTRY=<your-registry-name>

# Frontend Web App (build context is the exercise root — includes both Web + Web.Client projects)
az acr build \
  --registry $REGISTRY \
  --image azure-container-apps-exercise-502-web:latest \
  --file AzureContainerApps.Exercise502.Web/Dockerfile \
  .

# Quote Job
az acr build \
  --registry $REGISTRY \
  --image azure-container-apps-exercise-502-quote-job:latest \
  --file AzureContainerApps.Exercise502.QuoteJob/Dockerfile \
  ./AzureContainerApps.Exercise502.QuoteJob
```

### 2. Deploy with Bicep

```bash
az deployment group create \
  --resource-group <your-resource-group> \
  --template-file deployment/main.bicep \
  --parameters \
      webAppName=exercise-502-web \
      quoteJobName=exercise-502-quote-job \
      containerAppEnvironmentName=exercise-502-env \
      containerRegistryName=$REGISTRY \
      managedIdentityName=exercise-502-identity \
      blobStorageConnectionString="<your-blob-storage-connection-string>" \
      serviceBusConnectionString="<your-service-bus-connection-string>" \
      serviceBusQueueName="quote-requests" \
      azureOpenAiEndpoint="<your-azure-openai-endpoint>" \
      azureOpenAiApiKey="<your-azure-openai-api-key>" \
      azureOpenAiModelName="<your-model-deployment-name>" \
      webContainerImage=$REGISTRY.azurecr.io/azure-container-apps-exercise-502-web:latest \
      quoteJobContainerImage=$REGISTRY.azurecr.io/azure-container-apps-exercise-502-quote-job:latest
```

| Parameter | Description |
|-----------|-------------|
| `webAppName` | Name for the frontend Container App |
| `quoteJobName` | Name for the Container Job |
| `containerAppEnvironmentName` | Name of the Container App Environment |
| `containerRegistryName` | Azure Container Registry name (without `.azurecr.io`) |
| `managedIdentityName` | Name of the user-assigned managed identity |
| `blobStorageConnectionString` | Azure Storage connection string |
| `serviceBusConnectionString` | Azure Service Bus connection string |
| `serviceBusQueueName` | Queue name (default: `quote-requests`) |
| `azureOpenAiEndpoint` | Azure OpenAI resource endpoint URL |
| `azureOpenAiApiKey` | Azure OpenAI API key |
| `azureOpenAiModelName` | Deployment name (e.g. `gpt-4o`, `gpt-4o-mini`) |
| `webContainerImage` | Fully qualified image tag for the web app |
| `quoteJobContainerImage` | Fully qualified image tag for the quote job |

### What the Bicep creates

| Resource | Type | Description |
|----------|------|-------------|
| Container App Environment | `Microsoft.App/managedEnvironments` | Shared environment |
| Web Container App | `Microsoft.App/containerApps` | External ingress, 1–3 replicas |
| Quote Container Job | `Microsoft.App/jobs` | Event trigger, KEDA `azure-servicebus` scaler |
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
  --name exercise-502-quote-job \
  --resource-group <your-resource-group> \
  --output table
```

Trigger a manual execution (useful for testing without sending a Service Bus message):

```bash
az containerapp job start \
  --name exercise-502-quote-job \
  --resource-group <your-resource-group>
```

---

## Environment Variables

### Web App

| Variable | Description |
|----------|-------------|
| `BLOB_STORAGE_CONNECTION_STRING` | Azure Blob Storage connection string; used to read `quotes/quote502.json` |
| `SERVICE_BUS_CONNECTION_STRING` | Azure Service Bus connection string; used to send theme messages to the queue |
| `SERVICE_BUS_QUEUE_NAME` | Queue name (default: `quote-requests`) |

### Quote Job

| Variable | Description |
|----------|-------------|
| `BLOB_STORAGE_CONNECTION_STRING` | Azure Blob Storage connection string; used to write `quotes/quote502.json` |
| `SERVICE_BUS_CONNECTION_STRING` | Azure Service Bus connection string; used to receive theme messages |
| `SERVICE_BUS_QUEUE_NAME` | Queue name (default: `quote-requests`) |
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI resource endpoint URL |
| `AZURE_OPENAI_API_KEY` | Azure OpenAI API key |
| `AZURE_OPENAI_MODEL_NAME` | Model deployment name (e.g. `gpt-4o`, `gpt-4o-mini`) |
| `QUOTE_THEME` | *(Local dev only)* Override theme; bypasses Service Bus entirely |
