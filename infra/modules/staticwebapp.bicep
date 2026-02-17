// =============================================================================
// Module: staticwebapp.bicep
// Description: Azure Static Web App (Free tier) for the React SPA front-end
//              of the Secure File Transfer project.
// =============================================================================

// Static Web Apps are not available in all regions. eastus2 is the closest
// supported region to eastus. Content is served from edge nodes globally.
@description('Azure region for the Static Web App (must be a supported SWA region).')
param location string = 'eastus2'

@description('Short project name used in resource naming.')
param projectName string

@description('Deployment environment (dev, staging, prod).')
param environment string

@description('Tags applied to all resources.')
param tags object

var baseName = 'sft-${projectName}-${environment}'

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: '${baseName}-spa'
  location: location
  tags: tags
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
