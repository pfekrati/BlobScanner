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

6 - TODO: VM instructions
