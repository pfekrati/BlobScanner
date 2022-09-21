namespace BlobScanner.ResultProcessor
{
	public interface IMetricsClient
	{
		void SendMetrics(int filesProcssed, int threatsDetected);
	}
}