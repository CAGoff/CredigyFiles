// =============================================================================
// Module: dns-records.bicep
// Description: Creates public DNS CNAME records in the credigy.com zone for
//              custom domain routing. Deployed cross-RG since the DNS zone
//              lives in rg-network-prod-eus.
// =============================================================================

targetScope = 'resourceGroup'

@description('Name of the public DNS zone.')
param publicDnsZoneName string = 'credigy.com'

@description('Subdomain for the SPA (e.g., "files" for files.credigy.com).')
param spaSubdomain string = 'files'

@description('Subdomain for the API (e.g., "files-api" for files-api.credigy.com).')
param apiSubdomain string = 'files-api'

@description('Default hostname of the SWA (e.g., ashy-pond-xxx.azurestaticapps.net).')
param swaDefaultHostname string

@description('Default hostname of the API (e.g., sft-credigyfiles-dev-api.azurewebsites.net).')
param apiDefaultHostname string

resource publicDnsZone 'Microsoft.Network/dnsZones@2018-05-01' existing = {
  name: publicDnsZoneName
}

// CNAME: files.credigy.com -> SWA default hostname
resource swaCname 'Microsoft.Network/dnsZones/CNAME@2018-05-01' = {
  parent: publicDnsZone
  name: spaSubdomain
  properties: {
    TTL: 3600
    CNAMERecord: {
      cname: swaDefaultHostname
    }
  }
}

// CNAME: files-api.credigy.com -> API default hostname
resource apiCname 'Microsoft.Network/dnsZones/CNAME@2018-05-01' = {
  parent: publicDnsZone
  name: apiSubdomain
  properties: {
    TTL: 3600
    CNAMERecord: {
      cname: apiDefaultHostname
    }
  }
}
