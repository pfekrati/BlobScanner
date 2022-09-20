using System;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Blobs;

namespace BlobScanner.ResultProcessor
{
    public class QuarantineClient : IQuarantineClient
    {
        private BlobContainerClient quarantineContainerClient;
        private DefaultAzureCredentialOptions credentialOptions;

        public QuarantineClient(Uri quarantineContainerUrl)
        {
            credentialOptions = new DefaultAzureCredentialOptions();
            credentialOptions.Diagnostics.IsLoggingEnabled = true;
            credentialOptions.Diagnostics.IsLoggingContentEnabled = true;
            quarantineContainerClient = new BlobContainerClient(quarantineContainerUrl, new DefaultAzureCredential(credentialOptions));
        }

        public async Task<Uri> Quarantine(Uri sourceBlobUrl)
        {
            var sourceClient = new BlobClient(sourceBlobUrl, new DefaultAzureCredential(credentialOptions));
            var quarantineClient = quarantineContainerClient.GetBlobClient(sourceClient.Name);
            using (var stream = await quarantineClient.OpenWriteAsync(true))
            {
                await sourceClient.DownloadToAsync(stream);
            }
            await sourceClient.DeleteAsync();
            return quarantineClient.Uri;
        }
    }
}