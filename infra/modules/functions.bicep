// =============================================================================
// Module: functions.bicep
// Description: Flex Consumption (FC1) Function App for the SFT notification and
//              provisioning functions. VNet integrated, with user-assigned
//              managed identities and managed-identity-based storage access.
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

@description('Name of the functions runtime storage account.')
param funcStorageAccountName string

@description('Blob endpoint URI of the functions runtime storage account.')
param funcStorageBlobUri string

@description('Queue endpoint URI of the functions runtime storage account.')
param funcStorageQueueUri string

@description('Table endpoint URI of the functions runtime storage account.')
param funcStorageTableUri string

@description('Name of the deployment blob container in func storage.')
param funcDeployContainerName string

@description('Blob endpoint URI of the app storage account.')
param appStorageBlobUri string

@description('Application Insights connection string.')
param appInsightsConnectionString string

@description('URL of the SPA portal for notification emails.')
param portalUrl string

@description('Resource ID of the subnet for VNet integration.')
param subnetId string

@description('Tags applied to all resources.')
param tags object

var baseName = 'sft-${projectName}-${environment}'

resource functionPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${baseName}-func-plan'
  location: location
  tags: tags
  kind: 'functionapp'
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: '${baseName}-func'
  location: location
  tags: tags
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
    virtualNetworkSubnetId: subnetId
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${funcStorageBlobUri}${funcDeployContainerName}'
          authentication: {
            type: 'SystemAssignedIdentity'
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: 40
        instanceMemoryMB: 2048
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '9.0'
      }
    }
    siteConfig: {
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'AzureWebJobsStorage__accountName'
          value: funcStorageAccountName
        }
        {
          name: 'AzureWebJobsStorage__credential'
          value: 'managedidentity'
        }
        {
          name: 'AzureWebJobsStorage__blobServiceUri'
          value: funcStorageBlobUri
        }
        {
          name: 'AzureWebJobsStorage__queueServiceUri'
          value: funcStorageQueueUri
        }
        {
          name: 'AzureWebJobsStorage__tableServiceUri'
          value: funcStorageTableUri
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
