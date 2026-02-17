// =============================================================================
// Module: eventgrid.bicep
// Description: Event Grid system topic on the app storage account with an
//              event subscription that routes blob events to the notification
//              Azure Function.
// =============================================================================

@description('Azure region for the Event Grid resources.')
param location string

@description('Short project name used in resource naming.')
param projectName string

@description('Deployment environment (dev, staging, prod).')
param environment string

@description('Resource ID of the app storage account.')
param storageAccountId string

@description('Resource ID of the notification Function App.')
param functionAppId string

@description('Tags applied to all resources.')
param tags object

var baseName = 'sft-${projectName}-${environment}'

resource systemTopic 'Microsoft.EventGrid/systemTopics@2023-12-15-preview' = {
  name: '${baseName}-evgt'
  location: location
  tags: tags
  properties: {
    source: storageAccountId
    topicType: 'Microsoft.Storage.StorageAccounts'
  }
}

resource eventSubscription 'Microsoft.EventGrid/systemTopics/eventSubscriptions@2023-12-15-preview' = {
  parent: systemTopic
  name: '${baseName}-evgs-notify'
  properties: {
    destination: {
      endpointType: 'AzureFunction'
      properties: {
        resourceId: '${functionAppId}/functions/NotificationTrigger'
        maxEventsPerBatch: 1
        preferredBatchSizeInKilobytes: 64
      }
    }
    filter: {
      includedEventTypes: [
        'Microsoft.Storage.BlobCreated'
        'Microsoft.Storage.BlobDeleted'
      ]
    }
    eventDeliverySchema: 'EventGridSchema'
    retryPolicy: {
      maxDeliveryAttempts: 30
      eventTimeToLiveInMinutes: 1440
    }
  }
}

@description('Resource ID of the Event Grid system topic.')
output systemTopicId string = systemTopic.id

@description('Name of the Event Grid system topic.')
output systemTopicName string = systemTopic.name
