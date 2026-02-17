// =============================================================================
// Module: monitoring-law.bicep
// Description: Log Analytics Workspace deployed to the shared security resource
//              group. Called from monitoring.bicep with cross-RG scope.
// =============================================================================

@description('Azure region for the workspace.')
param location string

@description('Base name prefix (e.g. sft-credigyfiles-dev).')
param baseName string

@description('Tags applied to the resource.')
param tags object

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${baseName}-law'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

@description('Resource ID of the Log Analytics Workspace.')
output logAnalyticsWorkspaceId string = logAnalyticsWorkspace.id
