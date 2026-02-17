// =============================================================================
// Script: onboard-third-party.bicep
// Description: Per-third-party onboarding template. Creates a dedicated blob
//              container and assigns Storage Blob Data Contributor RBAC role
//              to the API identity and optionally a third-party service principal.
//
// Usage:
//   az deployment group create \
//     --resource-group <rg-name> \
//     --template-file onboard-third-party.bicep \
//     --parameters storageAccountName=<name> \
//                  thirdPartyName=<name> \
//                  apiIdentityPrincipalId=<guid> \
//                  thirdPartyPrincipalId=<guid>
// =============================================================================

targetScope = 'resourceGroup'

@description('Name of the app storage account to create the container in.')
param storageAccountName string

@description('Name of the third party (used as container name, lowercase alphanumeric and hyphens).')
param thirdPartyName string

@description('Principal ID of the API managed identity for RBAC assignment.')
param apiIdentityPrincipalId string

@description('Optional principal ID of a third-party service principal for RBAC assignment. Leave empty to skip.')
param thirdPartyPrincipalId string = ''

// Storage Blob Data Contributor built-in role definition ID.
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource blobServices 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' existing = {
  parent: storageAccount
  name: 'default'
}

resource thirdPartyContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobServices
  name: thirdPartyName
  properties: {
    publicAccess: 'None'
  }
}

resource apiRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, thirdPartyName, apiIdentityPrincipalId, storageBlobDataContributorRoleId)
  scope: thirdPartyContainer
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: apiIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource thirdPartyRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(thirdPartyPrincipalId)) {
  name: guid(storageAccount.id, thirdPartyName, thirdPartyPrincipalId, storageBlobDataContributorRoleId)
  scope: thirdPartyContainer
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: thirdPartyPrincipalId
    principalType: 'ServicePrincipal'
  }
}

@description('Name of the created blob container.')
output containerName string = thirdPartyContainer.name

@description('Resource ID of the API RBAC role assignment.')
output apiRoleAssignmentId string = apiRoleAssignment.id
