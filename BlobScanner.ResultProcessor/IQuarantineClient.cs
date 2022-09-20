using System;
using System.Threading.Tasks;

namespace BlobScanner.ResultProcessor
{
    public interface IQuarantineClient
    {
        Task<Uri> Quarantine(Uri sourceBlobUrl);
    }
}