param sourceStorageAccountId string
param userAssignedManagedIdentity string = '${uniqueString(resourceGroup().id)}-mi'
param logAnalyticsWorkspace string = '${uniqueString(resourceGroup().id)}-law'
param applicationInsights string = '${uniqueString(resourceGroup().id)}-ai'
param serviceBusNamespace string = 'sb${uniqueString(resourceGroup().id)}-ns'
param eventGridTopic string = '${uniqueString(resourceGroup().id)}-topic'
param quarantineStorageAccount string = '${uniqueString(resourceGroup().id)}q'
param functionPlan string = '${uniqueString(resourceGroup().id)}-plan'
param functionApp string = '${uniqueString(resourceGroup().id)}-rp'
param location string = resourceGroup().location

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2022-01-31-preview' = {
    name: userAssignedManagedIdentity
    location: location
}

resource logs 'Microsoft.OperationalInsights/workspaces@2021-12-01-preview' = {
    name: logAnalyticsWorkspace
    location: location
    properties: {
        sku: {
            name: 'PerGB2018'
        }
    }
}

resource ai 'Microsoft.Insights/components@2020-02-02' = {
    name: applicationInsights
    location: location
    kind: 'web'
    properties: {
        Application_Type: 'web'
        WorkspaceResourceId: logs.id
    }
}

resource bus 'Microsoft.ServiceBus/namespaces@2022-01-01-preview' = {
    name: serviceBusNamespace
    location: location
    sku: {
        name: 'Standard'
        tier: 'Standard'
    }

    resource filesQueue 'queues@2022-01-01-preview' = {
        name: 'filesqueue'
    }

    resource resultsqueue 'queues@2022-01-01-preview' = {
      name: 'resultsqueue'
  }
}

resource events 'Microsoft.EventGrid/systemTopics@2022-06-15' = {
    name: eventGridTopic
    location: location
    properties: {
         source: sourceStorageAccountId
         topicType: 'Microsoft.Storage.StorageAccounts'
    }

    resource subscription 'eventSubscriptions' = {
        name: 'subscription'
        properties: {
            destination: {
                endpointType: 'ServiceBusQueue'
                properties: {
                    resourceId: bus::filesQueue.id
                }                
            }
            filter: {
                includedEventTypes: [
                    'Microsoft.Storage.BlobCreated'
                ]
            }
        }
    }
}

resource quarantine 'Microsoft.Storage/storageAccounts@2022-05-01' = {
    name: quarantineStorageAccount
    location: location
    sku: {
        name: 'Standard_LRS'
    }
    kind: 'StorageV2'
}

resource plan 'Microsoft.Web/serverfarms@2022-03-01' = {
    name: functionPlan
    location: location
    sku: {
        name: 'Y1'
        tier: 'Dynamic'
        size: 'Y1'
        family: 'Y'
        capacity: 0
    }
}

resource function 'Microsoft.Web/sites@2022-03-01' = {
  name: functionApp
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    // siteConfig: {
    //   linuxFxVersion: 'DOTNET|6.0'
    // }
  }
}
