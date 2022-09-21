using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BlobScanner.ResultProcessor
{
    public class ResultProcessor
    {
        private ILogAnalyticsClient logAnalyticsClient;
        private IMetricsClient metricsClient;
        private IQuarantineClient quarantineClient;

        public ResultProcessor(ILogAnalyticsClient logAnalyticsClient, IMetricsClient metricsClient, IQuarantineClient quarantineClient)
        {
            this.logAnalyticsClient = logAnalyticsClient;
            this.metricsClient = metricsClient;
            this.quarantineClient = quarantineClient;
        }

        [FunctionName("Process")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var results = JsonConvert.DeserializeObject<ScanResult[]>(requestBody);

            logAnalyticsClient.SendTelemetry(JsonConvert.SerializeObject(results));
            metricsClient.SendMetrics(results.Length, results.Count(x => x.IsThreat));

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
