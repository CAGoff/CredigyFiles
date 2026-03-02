// =============================================================================
// Module: apim.bicep
// Description: Azure API Management (Developer tier) with External VNet
//              integration and the Secure File Transfer API definition.
// =============================================================================

@description('Azure region for the APIM instance.')
param location string

@description('Short project name used in resource naming.')
param projectName string

@description('Deployment environment (dev, staging, prod).')
param environment string

@description('Publisher email address for APIM.')
param publisherEmail string = 'admin@contoso.com'

@description('Publisher organization name for APIM.')
param publisherName string = 'Secure File Transfer'

@description('Default hostname of the backend Web App.')
param backendWebAppHostname string

@description('Resource ID of the dedicated APIM subnet (must be /27, no delegation).')
param apimSubnetId string

@description('Tags applied to all resources.')
param tags object

var baseName = 'sft-${projectName}-${environment}'

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
    virtualNetworkType: 'External'
    virtualNetworkConfiguration: {
      subnetResourceId: apimSubnetId
    }
    publicIpAddressId: publicIp.id
  }
}

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

@description('Resource ID of the APIM instance.')
output apimId string = apim.id

@description('Gateway URL of the APIM instance.')
output apimGatewayUrl string = apim.properties.gatewayUrl

@description('Name of the APIM instance.')
output apimName string = apim.name
