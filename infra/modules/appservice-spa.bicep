// =============================================================================
// Module: appservice-spa.bicep
// Description: Web App for the React SPA front-end, hosted on the shared
//              App Service Plan (B1 Linux). Pulls nginx container from ACR
//              using system-assigned managed identity.
// =============================================================================

@description('Azure region for the Web App.')
param location string

@description('Short project name used in resource naming.')
param projectName string

@description('Deployment environment (dev, staging, prod).')
param environment string

@description('Resource ID of the existing App Service Plan (shared with the API).')
param appServicePlanId string

@description('ACR login server (e.g. myacr.azurecr.io).')
param acrLoginServer string

@description('Container image name in ACR.')
param acrImageName string = 'sft-spa'

@description('Container image tag.')
param acrImageTag string = 'latest'

@description('Resource ID of the subnet for VNet integration.')
param subnetId string

@description('Whether to allow public network access. Set to Disabled after private endpoint is configured.')
@allowed([
  'Enabled'
  'Disabled'
])
param publicNetworkAccess string = 'Enabled'

@description('Tags applied to all resources.')
param tags object

var baseName = 'sft-${projectName}-${environment}'

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: '${baseName}-spa'
  location: location
  tags: tags
  kind: 'app,linux,container'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlanId
    httpsOnly: true
    publicNetworkAccess: publicNetworkAccess
    virtualNetworkSubnetId: subnetId
    siteConfig: {
      linuxFxVersion: 'DOCKER|${acrLoginServer}/${acrImageName}:${acrImageTag}'
      acrUseManagedIdentityCreds: true
      alwaysOn: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'DOCKER_REGISTRY_SERVER_URL'
          value: 'https://${acrLoginServer}'
        }
        {
          name: 'WEBSITES_PORT'
          value: '80'
        }
        {
          name: 'DOCKER_ENABLE_CI'
          value: 'false'
        }
        {
          name: 'WEBSITE_PULL_IMAGE_OVER_VNET'
          value: 'true'
        }
      ]
    }
  }
}

@description('Resource ID of the SPA Web App.')
output webAppId string = webApp.id

@description('Default hostname of the SPA Web App.')
output webAppHostname string = webApp.properties.defaultHostName

@description('Name of the SPA Web App.')
output webAppName string = webApp.name

@description('Principal ID of the SPA system-assigned managed identity (for AcrPull RBAC).')
output systemPrincipalId string = webApp.identity.principalId
