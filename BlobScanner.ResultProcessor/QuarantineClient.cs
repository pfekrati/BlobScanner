using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Blobs;

namespace BlobScanner.ResultProcessor
{
    public class QuarantineClient : IQuarantineClient
    {
        private readonly QuarantineBehavior behavior;
        private readonly BlobContainerClient quarantineContainerClient;
        private readonly DefaultAzureCredentialOptions credentialOptions;

        public QuarantineClient(QuarantineBehavior behavior, string quarantineContainerUrl, string managedIdentityClientId = null)
        {
            credentialOptions = new DefaultAzureCredentialOptions();
            credentialOptions.Diagnostics.IsLoggingEnabled = true;
            credentialOptions.Diagnostics.IsLoggingContentEnabled = true;
            
            this.behavior = behavior;
            if (!String.IsNullOrEmpty(managedIdentityClientId))
                credentialOptions.ManagedIdentityClientId = managedIdentityClientId;
            if (!String.IsNullOrEmpty(quarantineContainerUrl))
                quarantineContainerClient = new BlobContainerClient(new Uri(quarantineContainerUrl), new DefaultAzureCredential(credentialOptions));
        }

        public async Task<string> Quarantine(ScanResult scanResult)
        {
            if (behavior == QuarantineBehavior.LogOnly)
                return $"No action was taken on {scanResult.BlobUrl}";

            var result = $"Deleted {scanResult.BlobUrl}";
            var sourceClient = new BlobClient(scanResult.BlobUrl, new DefaultAzureCredential(credentialOptions));
            if (behavior == QuarantineBehavior.Move)
            {
                var quarantineClient = quarantineContainerClient.GetBlobClient($"{sourceClient.Name}_{DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss")}");
                using (var stream = await quarantineClient.OpenWriteAsync(true))
                {
                    await sourceClient.DownloadToAsync(stream);
                }

                IDictionary<string, string> metadata = new Dictionary<string, string>();

                metadata.Add("BlobScanner_ScanDateTime", scanResult.Timestamp.ToString());
                metadata.Add("BlobScanner_IsSafe", (!scanResult.IsThreat).ToString());
                metadata.Add("BlobScanner_ScanResult", scanResult.Result.ToString());
                metadata.Add("BlobScanner_DetectionEngine", scanResult.DetectionEngineInfo.ToString());

                await quarantineClient.SetMetadataAsync(metadata);

                result = $"Quarantined {scanResult.BlobUrl} to {quarantineClient.Uri};";

            }
            await sourceClient.DeleteAsync();
            return result;
        }
    }
}