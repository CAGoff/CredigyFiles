// =============================================================================
// Module: monitoring.bicep
// Description: Log Analytics Workspace and Application Insights for
//              centralized monitoring of the Secure File Transfer project.
// =============================================================================

@description('Azure region for the monitoring resources.')
param location string

@description('Short project name used in resource naming.')
param projectName string

@description('Deployment environment (dev, staging, prod).')
param environment string

var baseName = 'sft-${projectName}-${environment}'

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${baseName}-law'
  location: location
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

@description('Instrumentation key of Application Insights.')
output appInsightsInstrumentationKey string = appInsights.properties.InstrumentationKey

@description('Connection string of Application Insights.')
output appInsightsConnectionString string = appInsights.properties.ConnectionString
