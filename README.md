# Deployment instructions

1 - Clone the repo

2 - Use an existing resource group or create a new one with the following command (replacing the values):
```
az group create -g blobscanner-rg -l canadacentral
```

3 - Run the following command to deploy the infrastructure (replacing the values):
```
az group deployment create -g blobscanner-rg --template-file .\main.bicep --parameters sourceStorageAccountId=/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/other-rg/providers/Microsoft.Storage/storageAccounts/sourceaccount adminUsername=blob adminPassword=*******
```

4 - Once the infrastructure is deployed, add the following secrets to the cloned repo:

| Secret Name | Description
|-------------|------------
| AZURE_FUNCTIONAPP_NAME | The name of the Function app deployed in the previous step |
| BLOBSCANNER_RESULTPROCESSOR_PUBLISH_PROFILE | The publish profile of the Function app deployed in the previous step |

5 - Run the BlobScanner.ResultProcessor pipeline to deploy the Azure Function

6 - Install .net 6 SDK on the VM. https://dotnet.microsoft.com/en-us/download/dotnet/6.0

7 - Download and unzip BlobScannerClient-latest.zip file from the repo

8 - Run the BlobScanner.ConsoleApp.exe file using  following 3 arguments. 1)Servie Bus Endpoint, 2) Integration Event Grid Topic Endpoint and 3) Application Insights
Connection String. (all can be collected from the resources created in step 3.
Example: c:\blobscanner\BlobScanner.ConsoleApp.exe yourServiceBusName.servicebus.windows.net https://yourIntegrationEventGridName.canadacentral-1.eventgrid.azure.net/api/events InstrumentationKey=xxxxxxxxxxxxxxxxxxxxxxxxxx;IngestionEndpoint=https://canadacentral-1.in.applicationinsights.azure.com/;LiveEndpoint=https://canadacentral.livediagnostics.monitor.azure.com/
