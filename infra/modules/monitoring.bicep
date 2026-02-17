// =============================================================================
// Module: monitoring.bicep
// Description: Log Analytics Workspace (deployed to shared security RG) and
//              Application Insights (in project RG) for centralized monitoring.
// =============================================================================

@description('Azure region for the monitoring resources.')
param location string

@description('Short project name used in resource naming.')
param projectName string

@description('Deployment environment (dev, staging, prod).')
param environment string

@description('Resource group where the Log Analytics Workspace should be created.')
param lawResourceGroup string

@description('Tags applied to all resources.')
param tags object

var baseName = 'sft-${projectName}-${environment}'

// LAW is deployed to the shared security resource group via a nested module
module law 'monitoring-law.bicep' = {
  name: 'monitoring-law'
  scope: resourceGroup(lawResourceGroup)
  params: {
    location: location
    baseName: baseName
    tags: tags
  }
}

// App Insights stays in the project resource group, linked to the cross-RG LAW
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${baseName}-ai'
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: law.outputs.logAnalyticsWorkspaceId
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

@description('Resource ID of the Log Analytics Workspace.')
output logAnalyticsWorkspaceId string = law.outputs.logAnalyticsWorkspaceId

@description('Resource ID of the Application Insights instance.')
output appInsightsId string = appInsights.id

@description('Connection string of Application Insights.')
output appInsightsConnectionString string = appInsights.properties.ConnectionString
