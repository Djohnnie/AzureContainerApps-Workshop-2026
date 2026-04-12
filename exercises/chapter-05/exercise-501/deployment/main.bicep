@description('The name of the frontend Blazor Web App Container App.')
param webAppName string

@description('The name of the Azure Container Job that generates quotes.')
param quoteJobName string

@description('The name of the Container App Environment.')
param containerAppEnvironmentName string

@description('The name of the Azure Container Registry (without .azurecr.io).')
param containerRegistryName string

@description('The name of the user-assigned managed identity.')
param managedIdentityName string

@description('The Azure Blob Storage connection string used by both the web app and the quote job.')
@secure()
param blobStorageConnectionString string

@description('The Azure OpenAI endpoint URL.')
param azureOpenAiEndpoint string

@description('The Azure OpenAI API key.')
@secure()
param azureOpenAiApiKey string

@description('The Azure OpenAI model/deployment name (e.g. gpt-4o).')
param azureOpenAiModelName string

@description('The frontend Web App container image (e.g. myregistry.azurecr.io/exercise-501-web:latest).')
param webContainerImage string

@description('The Quote Job container image (e.g. myregistry.azurecr.io/exercise-501-quote-job:latest).')
param quoteJobContainerImage string

@description('Azure region for all resources.')
param location string = resourceGroup().location

var acrPullRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '7f951dda-4ed3-4680-a7ca-43fe172d538d'
)

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: containerRegistryName
}

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
}

module acrPullRoleAssignment 'roleAssignment.bicep' = {
  name: 'acrPullRoleAssignment'
  params: {
    principalId: managedIdentity.properties.principalId
    roleDefinitionId: acrPullRoleDefinitionId
    containerRegistryName: containerRegistryName
  }
}

resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerAppEnvironmentName
  location: location
  properties: {
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

// Blazor Web App — serves the Quote of the Day frontend and proxies blob storage reads
resource webContainerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: webAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${managedIdentity.id}': {} }
  }
  properties: {
    environmentId: containerAppEnvironment.id
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      registries: [
        {
          server: containerRegistry.properties.loginServer
          identity: managedIdentity.id
        }
      ]
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
      }
      secrets: [
        {
          name: 'blob-storage-connection-string'
          value: blobStorageConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'web'
          image: webContainerImage
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            {
              name: 'BLOB_STORAGE_CONNECTION_STRING'
              secretRef: 'blob-storage-connection-string'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 5
        rules: [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '20'
              }
            }
          }
        ]
      }
    }
  }
  dependsOn: [acrPullRoleAssignment]
}

// Azure Container Job — scheduled every minute to generate a new AI quote and write it to blob storage
resource quoteContainerJob 'Microsoft.App/jobs@2024-03-01' = {
  name: quoteJobName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${managedIdentity.id}': {} }
  }
  properties: {
    environmentId: containerAppEnvironment.id
    workloadProfileName: 'Consumption'
    configuration: {
      triggerType: 'Schedule'
      replicaTimeout: 300
      replicaRetryLimit: 1
      scheduleTriggerConfig: {
        cronExpression: '*/1 * * * *'
        parallelism: 1
        replicaCompletionCount: 1
      }
      registries: [
        {
          server: containerRegistry.properties.loginServer
          identity: managedIdentity.id
        }
      ]
      secrets: [
        {
          name: 'blob-storage-connection-string'
          value: blobStorageConnectionString
        }
        {
          name: 'azure-openai-api-key'
          value: azureOpenAiApiKey
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'quote-job'
          image: quoteJobContainerImage
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            {
              name: 'BLOB_STORAGE_CONNECTION_STRING'
              secretRef: 'blob-storage-connection-string'
            }
            {
              name: 'AZURE_OPENAI_ENDPOINT'
              value: azureOpenAiEndpoint
            }
            {
              name: 'AZURE_OPENAI_API_KEY'
              secretRef: 'azure-openai-api-key'
            }
            {
              name: 'AZURE_OPENAI_MODEL_NAME'
              value: azureOpenAiModelName
            }
          ]
        }
      ]
    }
  }
  dependsOn: [acrPullRoleAssignment]
}

output webAppUrl string = 'https://${webContainerApp.properties.configuration.ingress.fqdn}'
output quoteJobName string = quoteContainerJob.name
