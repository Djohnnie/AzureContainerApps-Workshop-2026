@description('The name of the Container App.')
param containerAppName string

@description('The name of the Container App Environment.')
param containerAppEnvironmentName string

@description('The name of the Azure Container Registry (without .azurecr.io).')
param containerRegistryName string

@description('The fully qualified container image name (e.g. myregistry.azurecr.io/azure-container-apps-exercise-202:latest).')
param containerImage string

@description('The name of the Managed Identity used by the Container App.')
param managedIdentityName string

@description('The location for all resources.')
param location string = resourceGroup().location

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: containerRegistryName
}

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
}

var acrPullRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '7f951dda-4ed3-4680-a7ca-43fe172d538d'
)

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

// Step 1: Deploy the darkblue revision, initially receiving 100% of traffic.
module blueRevision 'modules/container-app-revision.bicep' = {
  name: 'deploy-revision-blue'
  params: {
    containerAppName: containerAppName
    containerAppEnvironmentId: containerAppEnvironment.id
    containerImage: containerImage
    managedIdentityId: managedIdentity.id
    containerRegistryServer: containerRegistry.properties.loginServer
    revisionSuffix: 'blue'
    backgroundColor: 'darkblue'
    trafficWeights: [
      {
        revisionName: '${containerAppName}--blue'
        weight: 100
        latestRevision: false
      }
    ]
    location: location
  }
  dependsOn: [acrPullRoleAssignment]
}

// Step 2: Deploy the darkgreen revision and apply the 25% / 75% traffic split.
module greenRevision 'modules/container-app-revision.bicep' = {
  name: 'deploy-revision-green'
  params: {
    containerAppName: containerAppName
    containerAppEnvironmentId: containerAppEnvironment.id
    containerImage: containerImage
    managedIdentityId: managedIdentity.id
    containerRegistryServer: containerRegistry.properties.loginServer
    revisionSuffix: 'green'
    backgroundColor: 'darkgreen'
    trafficWeights: [
      {
        revisionName: '${containerAppName}--blue'
        weight: 25
        latestRevision: false
      }
      {
        revisionName: '${containerAppName}--green'
        weight: 75
        latestRevision: false
      }
    ]
    location: location
  }
  dependsOn: [blueRevision]
}

output containerAppFqdn string = greenRevision.outputs.containerAppFqdn
