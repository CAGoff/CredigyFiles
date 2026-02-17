// =============================================================================
// Module: staticwebapp.bicep
// Description: Azure Static Web App (Free tier) for the React SPA front-end
//              of the Secure File Transfer project.
// =============================================================================

@description('Azure region for the Static Web App.')
param location string

@description('Short project name used in resource naming.')
param projectName string

@description('Deployment environment (dev, staging, prod).')
param environment string

var baseName = 'sft-${projectName}-${environment}'

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: '${baseName}-spa'
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    stagingEnvironmentPolicy: 'Enabled'
    allowConfigFileUpdates: true
    buildProperties: {
      appLocation: '/'
      outputLocation: 'dist'
    }
  }
}

@description('Resource ID of the Static Web App.')
output staticWebAppId string = staticWebApp.id

@description('Default hostname of the Static Web App.')
output staticWebAppHostname string = staticWebApp.properties.defaultHostname

@description('Name of the Static Web App.')
output staticWebAppName string = staticWebApp.name
