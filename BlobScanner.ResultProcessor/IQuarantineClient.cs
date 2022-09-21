using System;
using System.Threading.Tasks;

namespace BlobScanner.ResultProcessor
{
    public interface IQuarantineClient
    {
        Task<string> Quarantine(Uri sourceBlobUrl);
    }
}