@description('The name of the Orleans Host Container App.')
param orleansHostAppName string

@description('The name of the API Container App.')
param apiAppName string

@description('The name of the Worker Container App.')
param workerAppName string

@description('The name of the Container App Environment.')
param containerAppEnvironmentName string

@description('The name of the Azure Container Registry (without .azurecr.io).')
param containerRegistryName string

@description('The name of the user-assigned managed identity.')
param managedIdentityName string

@description('The Azure Storage connection string used by Orleans for Table Storage clustering.')
@secure()
param storageConnectionString string

@description('The Orleans Host container image (e.g. myregistry.azurecr.io/azure-container-apps-exercise-301-orleans-host:latest).')
param orleansHostContainerImage string

@description('The API container image (e.g. myregistry.azurecr.io/azure-container-apps-exercise-301-api:latest).')
param apiContainerImage string

@description('The Worker container image (e.g. myregistry.azurecr.io/azure-container-apps-exercise-301-worker:latest).')
param workerContainerImage string

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

resource orleansHostContainerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: orleansHostAppName
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
          name: 'storage-connection-string'
          value: storageConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'orleans-host'
          image: orleansHostContainerImage
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            {
              name: 'AZURE_STORAGE_CONNECTION_STRING'
              secretRef: 'storage-connection-string'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 2
        maxReplicas: 2
      }
    }
  }
  dependsOn: [acrPullRoleAssignment]
}

resource apiContainerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: apiAppName
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
        external: false
        targetPort: 8080
        transport: 'auto'
      }
      secrets: [
        {
          name: 'storage-connection-string'
          value: storageConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiContainerImage
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            {
              name: 'AZURE_STORAGE_CONNECTION_STRING'
              secretRef: 'storage-connection-string'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 10
        rules: [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '10'
              }
            }
          }
        ]
      }
    }
  }
  dependsOn: [acrPullRoleAssignment]
}

resource workerContainerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: workerAppName
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
    }
    template: {
      containers: [
        {
          name: 'worker'
          image: workerContainerImage
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            {
              name: 'API_BASE_URL'
              value: 'https://${apiContainerApp.properties.configuration.ingress.fqdn}'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
  dependsOn: [acrPullRoleAssignment]
}

output orleansHostUrl string = 'https://${orleansHostContainerApp.properties.configuration.ingress.fqdn}/dashboard'
output apiInternalFqdn string = apiContainerApp.properties.configuration.ingress.fqdn
