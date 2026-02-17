// =============================================================================
// Module: functions.bicep
// Description: Consumption App Service Plan and Function App for the SFT
//              notification and provisioning functions. Both user-assigned
//              managed identities are assigned to the Function App.
// =============================================================================

@description('Azure region for the Function App resources.')
param location string

@description('Short project name used in resource naming.')
param projectName string

@description('Deployment environment (dev, staging, prod).')
param environment string

@description('Resource ID of the notification function managed identity.')
param notifyIdentityId string

@description('Client ID of the notification function managed identity.')
param notifyIdentityClientId string

@description('Resource ID of the provisioning function managed identity.')
param provisionIdentityId string

@description('Client ID of the provisioning function managed identity.')
param provisionIdentityClientId string

@description('Connection string for the functions runtime storage account.')
param funcStorageConnectionString string

@description('Blob endpoint URI of the app storage account.')
param appStorageBlobUri string

@description('Resource ID of the Application Insights instance.')
param appInsightsInstrumentationKey string

@description('Application Insights connection string.')
param appInsightsConnectionString string

@description('URL of the SPA portal for notification emails.')
param portalUrl string

var baseName = 'sft-${projectName}-${environment}'

resource functionPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${baseName}-func-plan'
  location: location
  kind: 'functionapp'
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: '${baseName}-func'
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${notifyIdentityId}': {}
      '${provisionIdentityId}': {}
    }
  }
  properties: {
    serverFarmId: functionPlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|9.0'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: funcStorageConnectionString
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'AppStorageUri'
          value: appStorageBlobUri
        }
        {
          name: 'NotifyIdentityClientId'
          value: notifyIdentityClientId
        }
        {
          name: 'ProvisionIdentityClientId'
          value: provisionIdentityClientId
        }
        {
          name: 'AcsConnectionString'
          value: 'PLACEHOLDER_ACS_CONNECTION_STRING'
        }
        {
          name: 'AcsSenderAddress'
          value: 'PLACEHOLDER_SENDER@your-domain.com'
        }
        {
          name: 'PortalUrl'
          value: portalUrl
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

@description('Resource ID of the Function App.')
output functionAppId string = functionApp.id

@description('Default hostname of the Function App.')
output functionAppHostname string = functionApp.properties.defaultHostName

@description('Name of the Function App.')
output functionAppName string = functionApp.name
