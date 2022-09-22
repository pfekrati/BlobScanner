param sourceStorageAccountId string
param userAssignedManagedIdentity string = '${uniqueString(resourceGroup().id)}-mi'
param logAnalyticsWorkspace string = '${uniqueString(resourceGroup().id)}-law'
param applicationInsights string = '${uniqueString(resourceGroup().id)}-ai'
param serviceBusNamespace string = 'sb${uniqueString(resourceGroup().id)}-ns'
param eventGridTopic string = '${uniqueString(resourceGroup().id)}-topic'
param quarantineStorageAccount string = '${uniqueString(resourceGroup().id)}q'
param functionPlan string = '${uniqueString(resourceGroup().id)}-cp'
param functionApp string = '${uniqueString(resourceGroup().id)}-f'
param functionAppStorage string = '${uniqueString(resourceGroup().id)}f'
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

resource wb 'Microsoft.Insights/workbooks@2022-04-01' = {
    name: guid(resourceGroup().id)
    location: location
    kind: 'shared'
    properties: {
        category: 'workbook'
        displayName: 'Blob Scanner'                
        serializedData: '{ "version": "Notebook/1.0", "items": [ { "type": 12, "content": { "version": "NotebookGroup/1.0", "groupType": "editable", "items": [ { "type": 1, "content": { "json": "<h1>Overview (last 7 days)</h1>" }, "name": "text - 0" }, { "type": 10, "content": { "chartId": "workbook34b61514-2a9c-4716-b4fa-2d4b57958d07", "version": "MetricsItem/2.0", "size": 4, "chartType": -1, "resourceType": "microsoft.insights/components", "metricScope": 0, "resourceIds": [ "${ai.id}" ], "timeContext": { "durationMs": 604800000 }, "metrics": [ { "namespace": "azure.applicationinsights", "metric": "azure.applicationinsights--FilesProcessed", "aggregation": 1, "columnName": "Files processed" }, { "namespace": "azure.applicationinsights", "metric": "azure.applicationinsights--ThreatsDetected", "aggregation": 1, "columnName": "Threats detected" } ], "gridFormatType": 1, "tileSettings": { "titleContent": { "columnMatch": "Metric", "formatter": 1 }, "leftContent": { "columnMatch": "Value", "formatter": 12, "formatOptions": { "palette": "auto" }, "numberFormat": { "unit": 17, "options": { "maximumSignificantDigits": 3, "maximumFractionDigits": 2 } } }, "showBorder": false }, "gridSettings": { "rowLimit": 10000 } }, "name": "metric - 1" } ] }, "name": "Overview" }, { "type": 12, "content": { "version": "NotebookGroup/1.0", "groupType": "editable", "items": [ { "type": 1, "content": { "json": "<h1>Logs</h1>" }, "name": "text - 0" }, { "type": 3, "content": { "version": "KqlItem/1.0", "query": "ScanResult_CL | sort by Timestamp_t desc | project Timestamp_t, BlobName_s, BlobUrl_s, IsThreat_b, Result_s", "size": 0, "timeContext": { "durationMs": 604800000 }, "queryType": 0, "resourceType": "microsoft.operationalinsights/workspaces", "crossComponentResources": [ "${logs.id}" ] }, "name": "query - 2" } ] }, "name": "Logs" } ], "isLocked": false, "fallbackResourceIds": [ "${ai.id}" ] }'
        sourceId: ai.id        
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

    resource blob 'blobServices' = {
        name: 'default'

        resource container 'containers' = {
            name: 'quarantine'
        }
    }
}

resource plan 'Microsoft.Web/serverfarms@2022-03-01' = {
    name: functionPlan
    location: location
    kind: 'functionapp'
    sku: {
        name: 'Y1'
        tier: 'Dynamic'
        size: 'Y1'
        family: 'Y'
        capacity: 0
    }
    properties: {
        reserved: true // Linux
    }
}

resource functionStorage 'Microsoft.Storage/storageAccounts@2022-05-01' = {
    name: functionAppStorage
    location: location
    sku: {
        name: 'Standard_LRS'
    }
    kind: 'StorageV2'

    resource files 'fileServices' = {
        name: 'default'

        resource share 'shares' = {
            name: 'web-content'
        }
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
    siteConfig: {
      linuxFxVersion: 'dotnet|6.0'
    }
  }

  resource settings 'config' = {
    name: 'appsettings'
    properties: {
        APPINSIGHTS_INSTRUMENTATIONKEY: ai.properties.InstrumentationKey
        APPLICATIONINSIGHTS_CONNECTION_STRING: ai.properties.ConnectionString
        AzureWebJobsStorage: 'DefaultEndpointsProtocol=https;AccountName=${functionStorage.name};AccountKey=${functionStorage.listkeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        WEBSITE_CONTENTAZUREFILECONNECTIONSTRING: 'DefaultEndpointsProtocol=https;AccountName=${functionStorage.name};AccountKey=${functionStorage.listkeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        WEBSITE_CONTENTSHARE: functionStorage::files::share.name
        WEBSITE_RUN_FROM_PACKAGE: '1'
        FUNCTIONS_EXTENSION_VERSION: '~4'
        FUNCTIONS_WORKER_RUNTIME: 'dotnet'
        LogAnalyticsCustomerId: logs.properties.customerId
        LogAnalyticsSharedKey: logs.listKeys().primarySharedKey
        ManagedIdentityClientId: identity.properties.clientId
        QuarantineBehavior: 'Move'
        QuarantineContainerUrl: '${quarantine.properties.primaryEndpoints.blob}${quarantine::blob::container.name}'
        ServiceBusConnection__clientID: identity.properties.clientId
        ServiceBusConnection__credential: 'managedidentity'
        ServiceBusConnection__fullyQualifiedNamespace: bus.name
        ServiceBusQueue: bus::resultsqueue.name
    }
  }
}
