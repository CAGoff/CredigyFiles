// =============================================================================
// Module: identity.bicep
// Description: User-assigned managed identities for the Secure File Transfer
//              project. Creates identities for the API, notification function,
//              and provisioning function.
// =============================================================================

@description('Azure region for the managed identities.')
param location string

@description('Short project name used in resource naming.')
param projectName string

@description('Deployment environment (dev, staging, prod).')
param environment string

var baseName = 'sft-${projectName}-${environment}'

resource identityApi 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${baseName}-id-sft-api'
  location: location
}

resource identityFuncNotify 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${baseName}-id-sft-func-notify'
  location: location
}

resource identityFuncProvision 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${baseName}-id-sft-func-provision'
  location: location
}

@description('Resource ID of the API managed identity.')
output apiIdentityId string = identityApi.id

@description('Client ID of the API managed identity.')
output apiIdentityClientId string = identityApi.properties.clientId

@description('Principal ID of the API managed identity.')
output apiIdentityPrincipalId string = identityApi.properties.principalId

@description('Resource ID of the notification function managed identity.')
output notifyIdentityId string = identityFuncNotify.id

@description('Client ID of the notification function managed identity.')
output notifyIdentityClientId string = identityFuncNotify.properties.clientId

@description('Principal ID of the notification function managed identity.')
output notifyIdentityPrincipalId string = identityFuncNotify.properties.principalId

@description('Resource ID of the provisioning function managed identity.')
output provisionIdentityId string = identityFuncProvision.id

@description('Client ID of the provisioning function managed identity.')
output provisionIdentityClientId string = identityFuncProvision.properties.clientId

@description('Principal ID of the provisioning function managed identity.')
output provisionIdentityPrincipalId string = identityFuncProvision.properties.principalId
