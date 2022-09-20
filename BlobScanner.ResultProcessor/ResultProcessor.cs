using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BlobScanner.ResultProcessor
{
    public class ResultProcessor
    {
        private ILogAnalyticsClient logAnalyticsClient;
        private IQuarantineClient quarantineClient;

        public ResultProcessor(ILogAnalyticsClient logAnalyticsClient, IQuarantineClient quarantineClient)
        {
            this.logAnalyticsClient = logAnalyticsClient;
            this.quarantineClient = quarantineClient;
        }

        [FunctionName("Process")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var results = JsonConvert.DeserializeObject<ScanResult[]>(requestBody);

            logAnalyticsClient.SendTelemetry(JsonConvert.SerializeObject(results));

            foreach (var result in results)
            {
                if (result.IsThreat)
                {
                    var quarantineUrl = await quarantineClient.Quarantine(result.BlobUrl);
                    result.Result = $"Quarantined to {quarantineUrl}";
                    logAnalyticsClient.SendTelemetry(JsonConvert.SerializeObject(result));
                }
            }

            return new OkResult();
        }
    }
}
