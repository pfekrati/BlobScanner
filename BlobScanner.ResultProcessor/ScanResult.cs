using System;

namespace BlobScanner.ResultProcessor
{
    public class ScanResult
    {
        public DateTime Timestamp { get; set; }
        public string BlobName { get; set; }
        public Uri BlobUrl { get; set; }
        public bool IsThreat { get; set; }
        public string Result { get; set; }
    }
}
