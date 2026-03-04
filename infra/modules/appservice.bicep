// =============================================================================
// Module: appservice.bicep
// Description: App Service Plan (B1 Linux) and Web App for the SFT API.
//              VNet integrated, with user-assigned managed identity and
//              app settings for storage and Azure AD authentication.
//              Pulls container images from ACR using managed identity.
// =============================================================================

@description('Azure region for the App Service resources.')
param location string

@description('Short project name used in resource naming.')
param projectName string

@description('Deployment environment (dev, staging, prod).')
param environment string

@description('Resource ID of the API user-assigned managed identity.')
param apiIdentityId string

@description('Client ID of the API user-assigned managed identity.')
param apiIdentityClientId string

@description('Blob endpoint URI of the app storage account (business data).')
param appStorageBlobUri string

@description('Application Insights connection string.')
param appInsightsConnectionString string

@description('Resource ID of the subnet for VNet integration.')
param subnetId string

@description('ACR login server (e.g. myacr.azurecr.io).')
param acrLoginServer string

@description('Container image name in ACR.')
param acrImageName string = 'sft-api'

@description('Container image tag.')
param acrImageTag string = 'latest'

@description('Azure AD tenant ID for JWT validation.')
param aadTenantId string = '00000000-0000-0000-0000-000000000000'

@description('Azure AD client ID of the API app registration.')
param aadApiClientId string = '00000000-0000-0000-0000-000000000000'

@description('Azure AD audience for the API (typically the API app registration client ID or URI).')
param aadApiAudience string = '00000000-0000-0000-0000-000000000000'

@description('Whether to allow public network access. Set to Disabled after private endpoint is configured.')
@allowed([
  'Enabled'
  'Disabled'
])
param publicNetworkAccess string = 'Enabled'

@description('Tags applied to all resources.')
param tags object

var baseName = 'sft-${projectName}-${environment}'

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${baseName}-plan'
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: '${baseName}-api'
  location: location
  tags: tags
  kind: 'app,linux,container'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${apiIdentityId}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    publicNetworkAccess: publicNetworkAccess
    virtualNetworkSubnetId: subnetId
    siteConfig: {
      linuxFxVersion: 'DOCKER|${acrLoginServer}/${acrImageName}:${acrImageTag}'
      acrUseManagedIdentityCreds: true
      acrUserManagedIdentityID: apiIdentityClientId
      alwaysOn: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'AZURE_CLIENT_ID'
          value: apiIdentityClientId
        }
        {
          name: 'Storage__AccountUri'
          value: appStorageBlobUri
        }
        {
          name: 'AzureAd__Instance'
          value: '${az.environment().authentication.loginEndpoint}/'
        }
        {
          name: 'AzureAd__TenantId'
          value: aadTenantId
        }
        {
          name: 'AzureAd__ClientId'
          value: aadApiClientId
        }
        {
          name: 'AzureAd__Audience'
          value: aadApiAudience
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'DOCKER_REGISTRY_SERVER_URL'
          value: 'https://${acrLoginServer}'
        }
        {
          name: 'WEBSITES_PORT'
          value: '8080'
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

@description('Resource ID of the App Service Plan.')
output appServicePlanId string = appServicePlan.id

@description('Resource ID of the Web App.')
output webAppId string = webApp.id

@description('Default hostname of the Web App.')
output webAppHostname string = webApp.properties.defaultHostName

@description('Name of the Web App.')
output webAppName string = webApp.name
