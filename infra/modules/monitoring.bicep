// =============================================================================
// Module: monitoring.bicep
// Description: Log Analytics Workspace and Application Insights for centralized
//              monitoring. Both resources deploy to the project resource group.
// =============================================================================

@description('Azure region for the monitoring resources.')
param location string

@description('Short project name used in resource naming.')
param projectName string

@description('Deployment environment (dev, staging, prod).')
param environment string

@description('Tags applied to all resources.')
param tags object

var baseName = 'sft-${projectName}-${environment}'

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

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${baseName}-ai'
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

@description('Resource ID of the Log Analytics Workspace.')
output logAnalyticsWorkspaceId string = logAnalyticsWorkspace.id

@description('Resource ID of the Application Insights instance.')
output appInsightsId string = appInsights.id

@description('Connection string of Application Insights.')
output appInsightsConnectionString string = appInsights.properties.ConnectionString
