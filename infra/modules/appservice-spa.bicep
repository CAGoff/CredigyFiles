// =============================================================================
// Module: appservice-spa.bicep
// Description: Web App for the React SPA front-end, hosted on the shared
//              App Service Plan (B1 Linux). Uses pm2 to serve static files
//              with SPA client-side routing support.
// =============================================================================

@description('Azure region for the Web App.')
param location string

@description('Short project name used in resource naming.')
param projectName string

@description('Deployment environment (dev, staging, prod).')
param environment string

@description('Resource ID of the existing App Service Plan (shared with the API).')
param appServicePlanId string

@description('Blob endpoint URI of the app storage account.')
param appStorageBlobUri string

@description('Name of the deploy container in app storage for Run From Package.')
param deployContainerName string

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
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlanId
    httpsOnly: true
    publicNetworkAccess: publicNetworkAccess
    virtualNetworkSubnetId: subnetId
    siteConfig: {
      linuxFxVersion: 'NODE|20-lts'
      appCommandLine: 'pm2 serve /home/site/wwwroot --no-daemon --spa'
      alwaysOn: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '${appStorageBlobUri}${deployContainerName}/package.zip'
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

@description('Principal ID of the SPA system-assigned managed identity (for RBAC on deploy blob).')
output systemPrincipalId string = webApp.identity.principalId
