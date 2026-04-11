@description('The name of the Container App.')
param containerAppName string

@description('The resource ID of the Container App Environment.')
param containerAppEnvironmentId string

@description('The fully qualified container image name.')
param containerImage string

@description('The resource ID of the user-assigned managed identity.')
param managedIdentityId string

@description('The login server of the Azure Container Registry.')
param containerRegistryServer string

@description('The revision suffix (e.g. "blue" or "green").')
param revisionSuffix string

@description('The CSS background color for this revision (e.g. "darkblue" or "darkgreen").')
param backgroundColor string

@description('The traffic weight rules for this revision deployment.')
param trafficWeights array

@description('The location for the Container App.')
param location string

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityId}': {}
    }
  }
  properties: {
    environmentId: containerAppEnvironmentId
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Multiple'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        traffic: trafficWeights
      }
      registries: [
        {
          server: containerRegistryServer
          identity: managedIdentityId
        }
      ]
    }
    template: {
      revisionSuffix: revisionSuffix
      containers: [
        {
          name: containerAppName
          image: containerImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'BACKGROUND_COLOR'
              value: backgroundColor
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 10
      }
    }
  }
}

output containerAppFqdn string = containerApp.properties.configuration.ingress.fqdn
