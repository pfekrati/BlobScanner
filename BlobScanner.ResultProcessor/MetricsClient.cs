using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;

namespace BlobScanner.ResultProcessor
{
    public class MetricsClient : IMetricsClient
	{
        private TelemetryClient telemetryClient;
        
        public MetricsClient(TelemetryConfiguration telemetryConfiguration)
        {
            telemetryClient = new TelemetryClient(telemetryConfiguration);
        }

		public void SendMetrics(int filesProcssed, int threatsDetected)
		{
            telemetryClient.GetMetric("FilesProcessed").TrackValue(filesProcssed);
            if (threatsDetected > 0)
                telemetryClient.GetMetric("ThreatsDetected").TrackValue(threatsDetected);
        }
    }
}