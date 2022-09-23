param sourceStorageAccountId string
@secure()
param adminUsername string
@secure()
param adminPassword string

param userAssignedManagedIdentityName string = '${uniqueString(resourceGroup().id)}-mi'
param logAnalyticsWorkspaceName string = '${uniqueString(resourceGroup().id)}-law'
param applicationInsightsName string = '${uniqueString(resourceGroup().id)}-ai'
param serviceBusNamespaceName string = 'sb${uniqueString(resourceGroup().id)}-ns'
param eventGridTopicName string = '${uniqueString(resourceGroup().id)}-topic'
param quarantineStorageAccountName string = '${uniqueString(resourceGroup().id)}q'
param functionPlanName string = '${uniqueString(resourceGroup().id)}-ep'
param functionApp string = '${uniqueString(resourceGroup().id)}-f'
param functionAppStorageAccountName string = '${uniqueString(resourceGroup().id)}f'
param vnetName string = '${uniqueString(resourceGroup().id)}-net'
param nicName string = '${uniqueString(resourceGroup().id)}-nic'
param vmName string = '${uniqueString(resourceGroup().id)}-vm'
param location string = resourceGroup().location

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2022-01-31-preview' = {
    name: userAssignedManagedIdentityName
    location: location
}

resource logs 'Microsoft.OperationalInsights/workspaces@2021-12-01-preview' = {
    name: logAnalyticsWorkspaceName
    location: location
    properties: {
        sku: {
            name: 'PerGB2018'
        }
    }
}

resource ai 'Microsoft.Insights/components@2020-02-02' = {
    name: applicationInsightsName
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

resource servicebus 'Microsoft.ServiceBus/namespaces@2022-01-01-preview' = {
    name: serviceBusNamespaceName
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

resource receiverRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
    name: guid(resourceGroup().id, userAssignedManagedIdentityName, 'receiver')
    scope: servicebus
    properties: {
        principalId: identity.properties.principalId
        roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0') // Azure Service Bus Data Receiver
        principalType: 'ServicePrincipal'
    }
}

resource senderRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
    name: guid(resourceGroup().id, userAssignedManagedIdentityName, 'sender')
    scope: servicebus
    properties: {
        principalId: identity.properties.principalId
        roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39') // Azure Service Bus Data Sender
        principalType: 'ServicePrincipal'
    }
}

resource events 'Microsoft.EventGrid/systemTopics@2022-06-15' = {
    name: eventGridTopicName
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
                    resourceId: servicebus::filesQueue.id
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
    name: quarantineStorageAccountName
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

resource functionPlan 'Microsoft.Web/serverfarms@2022-03-01' = {
    name: functionPlanName
    location: location
    kind: 'elastic'
    sku: {
        name: 'EP1'
        tier: 'ElasticPremium'
        size: 'EP1'
        family: 'EP'
        capacity: 1
    }
    properties: {
        reserved: true // Linux
    }
}

// resource functionPlan 'Microsoft.Web/serverfarms@2022-03-01' = {
//     name: functionPlanName
//     location: location
//     kind: 'functionapp'
//     sku: {
//         name: 'Y1'
//         tier: 'Dynamic'
//         size: 'Y1'
//         family: 'Y'
//         capacity: 0
//     }
//     properties: {
//         reserved: true // Linux
//     }
// }

resource functionStorage 'Microsoft.Storage/storageAccounts@2022-05-01' = {
    name: functionAppStorageAccountName
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
    serverFarmId: functionPlan.id
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
        ServiceBusConnection__fullyQualifiedNamespace: '${servicebus.name}.servicebus.windows.net'
        ServiceBusQueue: servicebus::resultsqueue.name
    }
  }
}

resource vnet 'Microsoft.Network/virtualnetworks@2015-05-01-preview' = {
    name: vnetName
    location: location
    properties: {        
        addressSpace: {
            addressPrefixes: [
                '172.16.0.0/16'
            ]
        }
    }

    resource default 'subnets' = {
        name: 'default'
        properties: {
            addressPrefix: '172.16.0.0/24'
        }        
    }
}

resource nic 'Microsoft.Network/networkInterfaces@2022-01-01' = {
    name: nicName
    location: location
    properties: {
        ipConfigurations: [
            {
                name: 'ipconfig1'
                properties: {
                    subnet: {
                        id: vnet::default.id
                    }
                }
            }
        ]
    }
}

resource vm 'Microsoft.Compute/virtualMachines@2022-03-01' = {
    name: vmName
    location: location
    identity: {
        type: 'UserAssigned'
        userAssignedIdentities: {
            '${identity.id}': {}
        }
    }
    properties: {
        hardwareProfile: {
            vmSize: 'Standard_B4ms'
        }
        storageProfile: {
            imageReference: {
                publisher: 'microsoftwindowsdesktop'
                offer: 'windows-11'
                sku: 'win11-21h2-ent'
                version: 'latest'
            }
            osDisk: {
                createOption: 'FromImage'
                managedDisk: {
                    storageAccountType: 'Standard_LRS'
                }
            }
        }
        osProfile: {
            computerName: 'scan-vm'
            adminUsername: adminUsername
            adminPassword: adminPassword
        }
        networkProfile: {
            networkInterfaces: [
                {
                    id: nic.id
                }
            ]
        }
    }
}
