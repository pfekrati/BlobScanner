using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlobScanner.ConsoleApp.Model
{
    internal class ScanResultModel
    {
        public DateTime Timestamp { get; set; }
        public string BlobName { get; set; }
        public string BlobUrl { get; set; }
        public bool IsThreat { get; set; }
        public string Result { get; set; }
        public string ResultDetail { get; set; }
        public string DetectionEngineInfo { get; set; }

    }
}
