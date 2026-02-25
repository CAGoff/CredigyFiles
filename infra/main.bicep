// =============================================================================
// Main: main.bicep
// Description: Root orchestration template for the Secure File Transfer
//              project infrastructure. Deploys all modules in the correct
//              dependency order.
//
// Usage:
//   az deployment group create \
//     --resource-group rg-credigyfiles-dev-eus \
//     --template-file main.bicep \
//     --parameters projectName=credigyfiles environment=dev
// =============================================================================

targetScope = 'resourceGroup'

// ---------------------------------------------------------------------------
// Parameters
// ---------------------------------------------------------------------------

@description('Azure region for all resources. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('Short project name used in resource naming (lowercase, no special chars).')
@minLength(2)
@maxLength(13)
param projectName string

@description('Deployment environment (dev, staging, prod).')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environment string

@description('Publisher email for the APIM instance.')
param apimPublisherEmail string = 'admin@contoso.com'

@description('Publisher name for the APIM instance.')
param apimPublisherName string = 'Secure File Transfer'

@description('APIM outbound IP address for App Service IP restriction. Leave empty to skip IP restrictions (useful for initial deployment before APIM exists).')
param apimOutboundIp string = ''

@description('Resource group containing the shared VNet.')
param vnetResourceGroup string = 'rg-network-dev-eus'

@description('Name of the shared VNet for VNet integration.')
param vnetName string = 'vnet-webapp-dev-eus'

@description('Name of the existing subnet for App Service VNet integration.')
param appServiceSubnetName string = 'sn-credigyfiles-outbound-dev-172_23_17_192-28'

@description('Name of the existing subnet for Function App VNet integration (must be delegated to Microsoft.App/environments).')
param functionAppSubnetName string = 'sn-credigyfiles-func-outbound-dev-172_23_17_208-28'

@description('Azure AD tenant ID for JWT validation. Update after creating Entra ID app registrations.')
param aadTenantId string = '99fac1cb-b614-418f-a367-8002fcdf2b2f'

@description('Azure AD client ID of the API app registration.')
param aadApiClientId string = 'fa1dcccc-ce0d-463c-a28b-08085248ef2e'

@description('Azure AD audience for the API (must match the identifier URI on the app registration).')
param aadApiAudience string = 'api://fa1dcccc-ce0d-463c-a28b-08085248ef2e'

@description('Deploy Event Grid subscription. Set to false for initial deployment (function code must be deployed first).')
param deployEventGrid bool = false

@description('Custom domain for the SPA (e.g., files.credigy.com). Leave empty to skip custom domain binding.')
param spaCustomDomain string = ''

@description('Required tags applied to all resources.')
param tags object = {
  Environment: 'Development'
  Project: 'CredigyFiles'
  Owner: 'christopher.goff@credigy.com'
}

// ---------------------------------------------------------------------------
// Cross-resource-group references
// ---------------------------------------------------------------------------

resource vnet 'Microsoft.Network/virtualNetworks@2024-01-01' existing = {
  name: vnetName
  scope: resourceGroup(vnetResourceGroup)
}

resource appServiceSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-01-01' existing = {
  parent: vnet
  name: appServiceSubnetName
}

resource functionAppSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-01-01' existing = {
  parent: vnet
  name: functionAppSubnetName
}

// ---------------------------------------------------------------------------
// Modules
// ---------------------------------------------------------------------------

// 1. Monitoring — LAW + App Insights in the project RG
module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    projectName: projectName
    environment: environment
    tags: tags
  }
}

// 2. Managed Identities (no dependencies)
module identity 'modules/identity.bicep' = {
  name: 'identity'
  params: {
    location: location
    projectName: projectName
    environment: environment
    tags: tags
  }
}

// 3. App Storage Account (no dependencies)
module storageApp 'modules/storage-app.bicep' = {
  name: 'storageApp'
  params: {
    location: location
    projectName: projectName
    environment: environment
    tags: tags
  }
}

// 4. Functions Storage Account (no dependencies)
module storageFunc 'modules/storage-func.bicep' = {
  name: 'storageFunc'
  params: {
    location: location
    projectName: projectName
    environment: environment
    tags: tags
  }
}

// 5. Static Web App (Standard tier — supports custom domains + private endpoints)
module staticWebApp 'modules/staticwebapp.bicep' = {
  name: 'staticWebApp'
  params: {
    projectName: projectName
    environment: environment
    customDomain: spaCustomDomain
    tags: tags
  }
}

// 6. Communication Services (no dependencies)
module communication 'modules/communication.bicep' = {
  name: 'communication'
  params: {
    projectName: projectName
    environment: environment
    tags: tags
  }
}

// 7. App Service (depends on: identity, storageApp, monitoring)
module appService 'modules/appservice.bicep' = {
  name: 'appService'
  params: {
    location: location
    projectName: projectName
    environment: environment
    apiIdentityId: identity.outputs.apiIdentityId
    apiIdentityClientId: identity.outputs.apiIdentityClientId
    appStorageBlobUri: storageApp.outputs.blobEndpointUri
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    apimOutboundIp: apimOutboundIp
    aadTenantId: aadTenantId
    aadApiClientId: aadApiClientId
    aadApiAudience: aadApiAudience
    publicNetworkAccess: 'Enabled'
    subnetId: appServiceSubnet.id
    tags: tags
  }
}

// 8. Function App (depends on: identity, storageApp, storageFunc, monitoring)
module functions 'modules/functions.bicep' = {
  name: 'functions'
  params: {
    location: location
    projectName: projectName
    environment: environment
    notifyIdentityId: identity.outputs.notifyIdentityId
    notifyIdentityClientId: identity.outputs.notifyIdentityClientId
    provisionIdentityId: identity.outputs.provisionIdentityId
    provisionIdentityClientId: identity.outputs.provisionIdentityClientId
    funcStorageAccountName: storageFunc.outputs.storageAccountName
    funcStorageBlobUri: storageFunc.outputs.blobEndpointUri
    funcStorageQueueUri: storageFunc.outputs.queueEndpointUri
    funcStorageTableUri: storageFunc.outputs.tableEndpointUri
    funcDeployContainerName: storageFunc.outputs.deployContainerName
    appStorageBlobUri: storageApp.outputs.blobEndpointUri
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    portalUrl: !empty(spaCustomDomain) ? 'https://${spaCustomDomain}' : 'https://${staticWebApp.outputs.staticWebAppHostname}'
    subnetId: functionAppSubnet.id
    tags: tags
  }
}

// 9. RBAC — role assignments for managed identities on storage accounts
//    (depends on: identity, storageApp, storageFunc, functions)
module rbac 'modules/rbac.bicep' = {
  name: 'rbac'
  params: {
    apiPrincipalId: identity.outputs.apiIdentityPrincipalId
    notifyPrincipalId: identity.outputs.notifyIdentityPrincipalId
    provisionPrincipalId: identity.outputs.provisionIdentityPrincipalId
    functionAppSystemPrincipalId: functions.outputs.functionAppSystemPrincipalId
    appStorageAccountName: storageApp.outputs.storageAccountName
    funcStorageAccountName: storageFunc.outputs.storageAccountName
  }
}

// 10. APIM (depends on: appService)
module apim 'modules/apim.bicep' = {
  name: 'apim'
  params: {
    location: location
    projectName: projectName
    environment: environment
    publisherEmail: apimPublisherEmail
    publisherName: apimPublisherName
    backendWebAppHostname: appService.outputs.webAppHostname
    tags: tags
  }
}

// 11. Event Grid (depends on: storageApp, functions)
//     Conditional — skip on first deploy because the function code must be deployed first.
//     Re-deploy with deployEventGrid=true after the CD pipeline deploys function code.
module eventGrid 'modules/eventgrid.bicep' = if (deployEventGrid) {
  name: 'eventGrid'
  params: {
    location: location
    projectName: projectName
    environment: environment
    storageAccountId: storageApp.outputs.storageAccountId
    functionAppId: functions.outputs.functionAppId
    tags: tags
  }
}

// ---------------------------------------------------------------------------
// NOTE: Private endpoints (SWA + API) and DNS records (CNAMEs + private DNS)
//       are managed outside this template by the network team / manually.
//       Module files are kept in modules/ for reference:
//         - modules/private-endpoint.bicep
//         - modules/dns-records.bicep
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------

@description('Blob endpoint URI of the app storage account.')
output appStorageBlobUri string = storageApp.outputs.blobEndpointUri

@description('Queue endpoint URI of the app storage account.')
output appStorageQueueUri string = storageApp.outputs.queueEndpointUri

@description('Name of the app storage account.')
output appStorageAccountName string = storageApp.outputs.storageAccountName

@description('Client ID of the API managed identity.')
output apiIdentityClientId string = identity.outputs.apiIdentityClientId

@description('Client ID of the notification function managed identity.')
output notifyIdentityClientId string = identity.outputs.notifyIdentityClientId

@description('Client ID of the provisioning function managed identity.')
output provisionIdentityClientId string = identity.outputs.provisionIdentityClientId

@description('Principal ID of the API managed identity (for RBAC assignments).')
output apiIdentityPrincipalId string = identity.outputs.apiIdentityPrincipalId

@description('Default hostname of the API Web App.')
output apiWebAppHostname string = appService.outputs.webAppHostname

@description('Default hostname of the Function App.')
output functionAppHostname string = functions.outputs.functionAppHostname

@description('Default hostname of the Static Web App.')
output staticWebAppHostname string = staticWebApp.outputs.staticWebAppHostname

@description('APIM gateway URL.')
output apimGatewayUrl string = apim.outputs.apimGatewayUrl

@description('Application Insights connection string.')
output appInsightsConnectionString string = monitoring.outputs.appInsightsConnectionString

@description('Resource ID of the Static Web App (needed for private endpoint creation).')
output staticWebAppId string = staticWebApp.outputs.staticWebAppId

@description('Resource ID of the API Web App (needed for private endpoint creation).')
output webAppId string = appService.outputs.webAppId
