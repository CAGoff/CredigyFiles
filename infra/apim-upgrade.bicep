// =============================================================================
// Standalone: apim-upgrade.bicep
// Description: Upgrades the existing APIM instance from Consumption to
//              Developer tier with External VNet integration. Deploy this
//              template on its own — it only touches APIM-related resources.
//
// Usage:
//   Deploy via Azure Portal → Custom Deployment → Build your own template
//   Target resource group: rg-credigyfiles-dev-eus
//
// NOTE: Developer tier provisioning takes 30-45 minutes. Do not cancel.
//       Delete this file after verification and sync changes into apim.bicep.
// =============================================================================

targetScope = 'resourceGroup'

// ---------------------------------------------------------------------------
// Parameters
// ---------------------------------------------------------------------------

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Short project name used in resource naming.')
param projectName string = 'credigyfiles'

@description('Deployment environment.')
param environment string = 'dev'

@description('Resource group containing the shared VNet.')
param vnetResourceGroup string = 'rg-network-dev-eus'

@description('Name of the shared VNet.')
param vnetName string = 'vnet-webapp-dev-eus'

@description('Name of the dedicated /27 subnet for APIM (no delegation required).')
param apimSubnetName string = 'sn-credigyfiles-apim-inbound-dev-172_23_17_224-27'

@description('Publisher email address for APIM.')
param publisherEmail string = 'christopher.goff@credigy.com'

@description('Publisher organization name for APIM.')
param publisherName string = 'Secure File Transfer'

@description('Default hostname of the backend API Web App.')
param backendWebAppHostname string = 'sft-credigyfiles-dev-api.azurewebsites.net'

@description('Tags applied to all resources.')
param tags object = {
  Environment: 'Development'
  Project: 'CredigyFiles'
  Owner: 'christopher.goff@credigy.com'
}

// ---------------------------------------------------------------------------
// Variables
// ---------------------------------------------------------------------------

var baseName = 'sft-${projectName}-${environment}'

// ---------------------------------------------------------------------------
// Cross-RG references
// ---------------------------------------------------------------------------

resource vnet 'Microsoft.Network/virtualNetworks@2024-01-01' existing = {
  name: vnetName
  scope: resourceGroup(vnetResourceGroup)
}

resource apimSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-01-01' existing = {
  parent: vnet
  name: apimSubnetName
}

// ---------------------------------------------------------------------------
// NSG: Reuse existing nsg-apimanagement-dev-eus (in rg-network-dev-eus).
//      Associate it to the APIM subnet when creating the subnet.
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Public IP for APIM (required for External VNet mode)
// ---------------------------------------------------------------------------

resource publicIp 'Microsoft.Network/publicIPAddresses@2024-01-01' = {
  name: '${baseName}-apim-pip'
  location: location
  tags: tags
  sku: {
    name: 'Standard'
    tier: 'Regional'
  }
  properties: {
    publicIPAllocationMethod: 'Static'
    dnsSettings: {
      domainNameLabel: '${baseName}-apim'
    }
  }
}

// ---------------------------------------------------------------------------
// APIM — Developer tier with External VNet
// ---------------------------------------------------------------------------

resource apim 'Microsoft.ApiManagement/service@2023-09-01-preview' = {
  name: '${baseName}-apim'
  location: location
  tags: tags
  sku: {
    name: 'Developer'
    capacity: 1
  }
  properties: {
    publisherEmail: publisherEmail
    publisherName: publisherName
    // Deploy without VNet first, then add VNet integration after activation succeeds.
    // virtualNetworkType: 'External'
    // virtualNetworkConfiguration: {
    //   subnetResourceId: apimSubnet.id
    // }
    // publicIpAddressId: publicIp.id
  }
}

// Re-declare the existing API definition so it's not dropped during update
resource sftApi 'Microsoft.ApiManagement/service/apis@2023-09-01-preview' = {
  parent: apim
  name: 'sft-api'
  properties: {
    displayName: 'Secure File Transfer API'
    path: 'sft'
    protocols: [
      'https'
    ]
    serviceUrl: 'https://${backendWebAppHostname}'
    subscriptionRequired: true
    subscriptionKeyParameterNames: {
      header: 'Ocp-Apim-Subscription-Key'
      query: 'subscription-key'
    }
    apiType: 'http'
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------

@description('APIM gateway URL.')
output apimGatewayUrl string = apim.properties.gatewayUrl

@description('APIM public IP address.')
output apimPublicIp string = publicIp.properties.ipAddress

@description('APIM VNet integration status.')
output apimVnetType string = apim.properties.virtualNetworkType
