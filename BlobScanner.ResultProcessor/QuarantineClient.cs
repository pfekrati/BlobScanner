using System;
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

        public async Task<string> Quarantine(Uri sourceBlobUrl)
        {
            if (behavior == QuarantineBehavior.LogOnly)
                return $"No action was taken on {sourceBlobUrl}";

            var result = $"Deleted {sourceBlobUrl}";
            var sourceClient = new BlobClient(sourceBlobUrl, new DefaultAzureCredential(credentialOptions));
            if (behavior == QuarantineBehavior.Move)
            {
                var quarantineClient = quarantineContainerClient.GetBlobClient(sourceClient.Name);
                using (var stream = await quarantineClient.OpenWriteAsync(true))
                {
                    await sourceClient.DownloadToAsync(stream);
                }
                result = $"Quarantined {sourceBlobUrl} to {quarantineClient.Uri};";
            }
            await sourceClient.DeleteAsync();
            return result;
        }
    }
}