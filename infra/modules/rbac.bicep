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

@description('Principal ID of the SPA system-assigned identity (for Run From Package blob read).')
param spaSystemPrincipalId string

@description('Principal ID of the CD service principal (for uploading deploy packages to blob storage). Leave empty to skip.')
param cdServicePrincipalId string = ''

@description('Name of the app storage account (business data).')
param appStorageAccountName string

@description('Name of the functions runtime storage account.')
param funcStorageAccountName string

// Well-known built-in role definition IDs
var storageBlobDataContributor = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var storageBlobDataReader = '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'
var storageTableDataContributor = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
var storageQueueDataContributor = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
var storageTableDataReader = '76199698-9eea-4c19-bc75-cec21354c6b6'
var storageBlobDataOwner = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var storageAccountContributor = '17d1049b-9a84-46fb-8f53-869881c3d3ab'

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
// Function App system identity → func storage (full runtime access)
//   Blob Data Owner: deployment container + runtime coordination blobs
//   Queue Data Contributor: internal queues (blob trigger, runtime)
//   Table Data Contributor: runtime diagnostics and lease management
//   Storage Account Contributor: management operations (blob trigger)
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

resource funcSystemQueue 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(funcStorage.id, functionAppSystemPrincipalId, storageQueueDataContributor)
  scope: funcStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageQueueDataContributor)
    principalId: functionAppSystemPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource funcSystemTable 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(funcStorage.id, functionAppSystemPrincipalId, storageTableDataContributor)
  scope: funcStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageTableDataContributor)
    principalId: functionAppSystemPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource funcSystemMgmt 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(funcStorage.id, functionAppSystemPrincipalId, storageAccountContributor)
  scope: funcStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageAccountContributor)
    principalId: functionAppSystemPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ---------------------------------------------------------------------------
// SPA system identity → app storage (blob read for Run From Package)
// ---------------------------------------------------------------------------

resource spaBlob 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appStorage.id, spaSystemPrincipalId, storageBlobDataReader)
  scope: appStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataReader)
    principalId: spaSystemPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ---------------------------------------------------------------------------
// CD service principal → app storage (blob write for deploy package upload)
//   Conditional — skip when cdServicePrincipalId is not provided.
// ---------------------------------------------------------------------------

resource cdBlob 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(cdServicePrincipalId)) {
  name: guid(appStorage.id, cdServicePrincipalId, storageBlobDataContributor)
  scope: appStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributor)
    principalId: cdServicePrincipalId
    principalType: 'ServicePrincipal'
  }
}
