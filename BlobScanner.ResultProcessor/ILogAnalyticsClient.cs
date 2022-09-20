namespace BlobScanner.ResultProcessor
{
	public interface ILogAnalyticsClient
	{
		void SendTelemetry(string json);
	}
}