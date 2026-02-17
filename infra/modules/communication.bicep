// =============================================================================
// Module: communication.bicep
// Description: Azure Communication Services resource for sending email
//              notifications in the Secure File Transfer project.
// =============================================================================

@description('Short project name used in resource naming.')
param projectName string

@description('Deployment environment (dev, staging, prod).')
param environment string

var baseName = 'sft-${projectName}-${environment}'

// ACS is a global resource; location is always 'global'.
resource communicationService 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: '${baseName}-acs'
  location: 'global'
  properties: {
    dataLocation: 'United States'
  }
}

@description('Resource ID of the Communication Services resource.')
output communicationServiceId string = communicationService.id

@description('Name of the Communication Services resource.')
output communicationServiceName string = communicationService.name
