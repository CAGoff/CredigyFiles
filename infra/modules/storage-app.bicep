// =============================================================================
// Module: storage-app.bicep
// Description: App storage account for Secure File Transfer. Stores uploaded
//              files with lifecycle management and soft-delete policies.
// =============================================================================

@description('Azure region for the storage account.')
param location string

@description('Short project name used in resource naming.')
param projectName string

@description('Deployment environment (dev, staging, prod).')
param environment string

@description('Tags applied to all resources.')
param tags object

// Storage account names must be 3-24 chars, lowercase alphanumeric only.
var storageAccountName = replace('sft${projectName}${environment}app', '-', '')

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: length(storageAccountName) > 24 ? substring(storageAccountName, 0, 24) : storageAccountName
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    allowSharedKeyAccess: false
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    accessTier: 'Hot'
  }
}

resource blobServices 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: 14
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: 7
    }
  }
}

// Deploy containers for Run From Package (API and SPA zip packages)
var deployApiContainerName = 'deploy-api'
var deploySpaContainerName = 'deploy-spa'

resource deployApiContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobServices
  name: deployApiContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource deploySpaContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobServices
  name: deploySpaContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource queueServices 'Microsoft.Storage/storageAccounts/queueServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource lifecyclePolicy 'Microsoft.Storage/storageAccounts/managementPolicies@2023-05-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    policy: {
      rules: [
        {
          name: 'tierToCool'
          enabled: true
          type: 'Lifecycle'
          definition: {
            filters: {
              blobTypes: [
                'blockBlob'
              ]
            }
            actions: {
              baseBlob: {
                tierToCool: {
                  daysAfterModificationGreaterThan: 30
                }
              }
            }
          }
        }
        {
          name: 'tierToCold'
          enabled: true
          type: 'Lifecycle'
          definition: {
            filters: {
              blobTypes: [
                'blockBlob'
              ]
            }
            actions: {
              baseBlob: {
                tierToCold: {
                  daysAfterModificationGreaterThan: 90
                }
              }
            }
          }
        }
      ]
    }
  }
}

@description('Resource ID of the app storage account.')
output storageAccountId string = storageAccount.id

@description('Name of the app storage account.')
output storageAccountName string = storageAccount.name

@description('Blob endpoint URI for the app storage account.')
output blobEndpointUri string = storageAccount.properties.primaryEndpoints.blob

@description('Queue endpoint URI for the app storage account.')
output queueEndpointUri string = storageAccount.properties.primaryEndpoints.queue

@description('Name of the deploy container for API packages (Run From Package).')
output deployApiContainerName string = deployApiContainerName

@description('Name of the deploy container for SPA packages (Run From Package).')
output deploySpaContainerName string = deploySpaContainerName
