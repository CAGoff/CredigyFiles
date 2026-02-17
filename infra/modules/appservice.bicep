// =============================================================================
// Module: appservice.bicep
// Description: App Service Plan (B1 Linux) and Web App for the SFT API.
//              Assigns the API user-assigned managed identity and configures
//              app settings for storage and Azure AD authentication.
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

@description('Blob endpoint URI of the app storage account.')
param appStorageBlobUri string

@description('Resource ID of the Application Insights instance.')
param appInsightsInstrumentationKey string

@description('Application Insights connection string.')
param appInsightsConnectionString string

@description('APIM outbound IP address for IP restriction. Use APIM gateway IP or service tag.')
param apimOutboundIp string = ''

var baseName = 'sft-${projectName}-${environment}'

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${baseName}-plan'
  location: location
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
  kind: 'app,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${apiIdentityId}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      alwaysOn: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      ipSecurityRestrictions: empty(apimOutboundIp) ? [] : [
        {
          name: 'AllowAPIM'
          description: 'Allow traffic from APIM only.'
          ipAddress: apimOutboundIp
          action: 'Allow'
          priority: 100
        }
        {
          name: 'DenyAll'
          description: 'Deny all other traffic.'
          ipAddress: 'Any'
          action: 'Deny'
          priority: 200
        }
      ]
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
          value: 'https://login.microsoftonline.com/'
        }
        {
          name: 'AzureAd__TenantId'
          value: 'PLACEHOLDER_TENANT_ID'
        }
        {
          name: 'AzureAd__ClientId'
          value: 'PLACEHOLDER_API_APP_CLIENT_ID'
        }
        {
          name: 'AzureAd__Audience'
          value: 'PLACEHOLDER_API_AUDIENCE'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsightsInstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
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
