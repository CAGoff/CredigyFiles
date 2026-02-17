// =============================================================================
// Module: storage-func.bicep
// Description: Storage account for Azure Functions runtime internal state.
//              Shared key access is enabled as required by Functions runtime.
// =============================================================================

@description('Azure region for the storage account.')
param location string

@description('Short project name used in resource naming.')
param projectName string

@description('Deployment environment (dev, staging, prod).')
param environment string

// Storage account names must be 3-24 chars, lowercase alphanumeric only.
var storageAccountName = replace('sft${projectName}${environment}func', '-', '')

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name = length(storageAccountName) > 24 ? substring(storageAccountName, 0, 24) : storageAccountName
  location: location
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

@description('Resource ID of the functions storage account.')
output storageAccountId string = storageAccount.id

@description('Name of the functions storage account.')
output storageAccountName string = storageAccount.name

@description('Connection string for the functions storage account.')
output storageConnectionString string = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${az.environment().suffixes.storage}'
