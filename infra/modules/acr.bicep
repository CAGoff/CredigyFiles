// =============================================================================
// Module: acr.bicep
// Description: Azure Container Registry for API and SPA container images.
//              Uses managed identity authentication only (admin user disabled).
// =============================================================================

@description('Azure region for the Container Registry.')
param location string

@description('Short project name used in resource naming.')
param projectName string

@description('Deployment environment (dev, staging, prod).')
param environment string

@description('Tags applied to all resources.')
param tags object

// ACR names must be 5-50 chars, alphanumeric only.
var acrName = replace('sft${projectName}${environment}acr', '-', '')

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

@description('Login server URL of the Container Registry.')
output loginServer string = containerRegistry.properties.loginServer

@description('Resource ID of the Container Registry.')
output acrId string = containerRegistry.id
