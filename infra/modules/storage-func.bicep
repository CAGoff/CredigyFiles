// =============================================================================
// Module: storage-func.bicep
// Description: Storage account for Azure Functions runtime internal state.
//              Shared key access is enabled as required by Functions runtime.
//              Includes a deployment container for Flex Consumption packages.
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
var storageAccountName = replace('sft${projectName}${environment}func', '-', '')
var deployContainerName = 'app-package-${projectName}'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: length(storageAccountName) > 24 ? substring(storageAccountName, 0, 24) : storageAccountName
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    allowSharedKeyAccess: true
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

resource blobServices 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource deployContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobServices
  name: deployContainerName
  properties: {
    publicAccess: 'None'
  }
}

@description('Resource ID of the functions storage account.')
output storageAccountId string = storageAccount.id

@description('Name of the functions storage account.')
output storageAccountName string = storageAccount.name

@description('Blob endpoint URI of the functions storage account.')
output blobEndpointUri string = storageAccount.properties.primaryEndpoints.blob

@description('Queue endpoint URI of the functions storage account.')
output queueEndpointUri string = storageAccount.properties.primaryEndpoints.queue

@description('Table endpoint URI of the functions storage account.')
output tableEndpointUri string = storageAccount.properties.primaryEndpoints.table

@description('Name of the deployment blob container for Flex Consumption.')
output deployContainerName string = deployContainerName
