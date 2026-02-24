// =============================================================================
// Module: rbac.bicep
// Description: RBAC role assignments granting managed identities access to
//              storage accounts. Covers API, notification, provisioning, and
//              Function App system identities.
// =============================================================================

@description('Principal ID of the API managed identity.')
param apiPrincipalId string

@description('Principal ID of the notification function managed identity.')
param notifyPrincipalId string

@description('Principal ID of the provisioning function managed identity.')
param provisionPrincipalId string

@description('Principal ID of the Function App system-assigned identity (for runtime storage).')
param functionAppSystemPrincipalId string

@description('Name of the app storage account (business data).')
param appStorageAccountName string

@description('Name of the functions runtime storage account.')
param funcStorageAccountName string

// Well-known built-in role definition IDs
var storageBlobDataContributor = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var storageTableDataContributor = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
var storageQueueDataContributor = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
var storageTableDataReader = '76199698-9eea-4c19-bc75-cec21354c6b6'
var storageBlobDataOwner = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'

// ---------------------------------------------------------------------------
// References to existing storage accounts (deployed by other modules)
// ---------------------------------------------------------------------------

resource appStorage 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: appStorageAccountName
}

resource funcStorage 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: funcStorageAccountName
}

// ---------------------------------------------------------------------------
// API identity → app storage (blob + table + queue)
// ---------------------------------------------------------------------------

resource apiBlob 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appStorage.id, apiPrincipalId, storageBlobDataContributor)
  scope: appStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributor)
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource apiTable 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appStorage.id, apiPrincipalId, storageTableDataContributor)
  scope: appStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageTableDataContributor)
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource apiQueue 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appStorage.id, apiPrincipalId, storageQueueDataContributor)
  scope: appStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageQueueDataContributor)
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ---------------------------------------------------------------------------
// Notify identity → app storage (table read only)
// ---------------------------------------------------------------------------

resource notifyTable 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appStorage.id, notifyPrincipalId, storageTableDataReader)
  scope: appStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageTableDataReader)
    principalId: notifyPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ---------------------------------------------------------------------------
// Provision identity → app storage (blob + table + queue)
// ---------------------------------------------------------------------------

resource provisionBlob 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appStorage.id, provisionPrincipalId, storageBlobDataContributor)
  scope: appStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributor)
    principalId: provisionPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource provisionTable 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appStorage.id, provisionPrincipalId, storageTableDataContributor)
  scope: appStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageTableDataContributor)
    principalId: provisionPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource provisionQueue 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appStorage.id, provisionPrincipalId, storageQueueDataContributor)
  scope: appStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageQueueDataContributor)
    principalId: provisionPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ---------------------------------------------------------------------------
// Function App system identity → func storage (blob owner for deploy + runtime)
// ---------------------------------------------------------------------------

resource funcSystemBlob 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(funcStorage.id, functionAppSystemPrincipalId, storageBlobDataOwner)
  scope: funcStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataOwner)
    principalId: functionAppSystemPrincipalId
    principalType: 'ServicePrincipal'
  }
}
