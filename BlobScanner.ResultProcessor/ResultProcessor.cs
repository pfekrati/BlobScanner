using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BlobScanner.ResultProcessor
{
    public class ResultProcessor
    {
        private readonly ILogAnalyticsClient logAnalyticsClient;
        private readonly IMetricsClient metricsClient;
        private readonly IQuarantineClient quarantineClient;

        public ResultProcessor(ILogAnalyticsClient logAnalyticsClient, IMetricsClient metricsClient, IQuarantineClient quarantineClient)
        {
            this.logAnalyticsClient = logAnalyticsClient;
            this.metricsClient = metricsClient;
            this.quarantineClient = quarantineClient;
        }

        [FunctionName("ProcessHTTP")]
        public async Task<IActionResult> RunHTTP(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var result = JsonConvert.DeserializeObject<ScanResult>(requestBody);
            await ProcessResults(result);
            return new OkResult();
        }

        [FunctionName("ProcessServiceBus")]
        public async Task RunServiceBus([ServiceBusTrigger("%ServiceBusQueue%", Connection = "ServiceBusConnection")] string msg, ILogger log)
        {
            var results = JsonConvert.DeserializeObject<ScanResult>(msg);
            await ProcessResults(results);
        }

        private async Task ProcessResults(ScanResult result)
        {
            logAnalyticsClient.SendTelemetry(JsonConvert.SerializeObject(result));
            metricsClient.SendMetrics(1, result.IsThreat ? 1 : 0);
            if (result.IsThreat)
            {
                result.Result = await quarantineClient.Quarantine(result.BlobUrl);
                logAnalyticsClient.SendTelemetry(JsonConvert.SerializeObject(result));
            }
        }
    }
}

