// =============================================================================
// Module: private-endpoint.bicep
// Description: Creates a private endpoint for a given resource with automatic
//              DNS A-record registration in an existing private DNS zone.
// =============================================================================

@description('Azure region for the private endpoint.')
param location string

@description('Name of the private endpoint resource.')
param privateEndpointName string

@description('Resource ID of the subnet for the private endpoint.')
param subnetId string

@description('Resource ID of the target resource to connect via private link.')
param privateLinkServiceId string

@description('Group ID for the private link connection (e.g., "sites" for App Service, "staticSites" for SWA).')
param groupId string

@description('Resource ID of the existing private DNS zone for automatic A-record registration.')
param privateDnsZoneId string

@description('Tags applied to all resources.')
param tags object

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2024-01-01' = {
  name: privateEndpointName
  location: location
  tags: tags
  properties: {
    subnet: {
      id: subnetId
    }
    privateLinkServiceConnections: [
      {
        name: privateEndpointName
        properties: {
          privateLinkServiceId: privateLinkServiceId
          groupIds: [
            groupId
          ]
        }
      }
    ]
  }
}

resource dnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-01-01' = {
  parent: privateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: '${groupId}-dns-config'
        properties: {
          privateDnsZoneId: privateDnsZoneId
        }
      }
    ]
  }
}

@description('Resource ID of the private endpoint.')
output privateEndpointId string = privateEndpoint.id
