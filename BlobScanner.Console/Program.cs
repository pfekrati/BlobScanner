using System;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Newtonsoft.Json;
using System.IO;
using MVsDotNetAMSIClient;
using System.Linq;
using MVsDotNetAMSIClient.Contracts;
using Azure.Messaging.EventGrid;
using BlobScanner.ConsoleApp.Model;
using System.Collections.Generic;
using System.Reflection.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights;

namespace BlobScanner.ConsoleApp
{
    class Program
    {
        const string queueName = "filesqueue";
        const string resultsQueueName = "resultsqueue";

        static BlobClient _blobClient;
        static ServiceBusClient _serviceBusClient;
        static ServiceBusSender _serviceBusSnder;
        static EventGridPublisherClient _eventGridclient;
        static ILogger<Program> _logger;
        static TelemetryClient _telemetryClient;

        static async Task Main(string[] args)
        {
            System.AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;

            var serviceBusServer = args[0];
            var eventGridServer = args[1];
            var appInsightsConnectionString = args[2];

            IServiceCollection services = new ServiceCollection();

            services.AddLogging(loggingBuilder => loggingBuilder.AddFilter<Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider>("Category", LogLevel.Information));
            services.AddApplicationInsightsTelemetryWorkerService(appInsightsConnectionString);
            IServiceProvider serviceProvider = services.BuildServiceProvider();
            _logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            var telemetryClient = serviceProvider.GetRequiredService<TelemetryClient>();

            _serviceBusClient = new ServiceBusClient(serviceBusServer, new DefaultAzureCredential());
            _serviceBusSnder = _serviceBusClient.CreateSender(resultsQueueName);
            _eventGridclient = new EventGridPublisherClient(new Uri(eventGridServer), new DefaultAzureCredential());

            ServiceBusProcessor processor = _serviceBusClient.CreateProcessor(queueName, new ServiceBusProcessorOptions());


            processor.ProcessMessageAsync += MessageHandler;
            processor.ProcessErrorAsync += ProcessErrorHandler;

            Console.WriteLine("Connecting to Service Bus ...");
            await processor.StartProcessingAsync();
            Console.WriteLine("Ready to receive messages ...");

            Console.ReadKey();

            await processor.StopProcessingAsync();

        }

        static async Task MessageHandler(ProcessMessageEventArgs args)
        {
                Console.WriteLine($"New message recieved. Timestamp: {DateTime.Now}");
                string body = args.Message.Body.ToString();

                var convertedBody = JsonConvert.DeserializeObject<dynamic>(body);

                string blobUrl = Convert.ToString(convertedBody.data.url);

                Console.WriteLine($"Downloading file {blobUrl}");

                await DownloadAndScanFile(blobUrl);

                await args.CompleteMessageAsync(args.Message);
        }

        static Task ProcessErrorHandler(ProcessErrorEventArgs eventArgs)
        {
            Console.WriteLine($"an unhandled exception was encountered. This was not expected to happen.");
            Console.WriteLine(eventArgs.Exception.ToString());
            _logger.LogError(eventArgs.Exception, eventArgs.Exception.Message, DateTimeOffset.Now);
            return Task.CompletedTask;
        }

        static async Task DownloadAndScanFile(string blobUrl)
        {
            var uri = new Uri(blobUrl);
            _blobClient = new BlobClient(uri, new DefaultAzureCredential());
            byte[] fileContent;

            using (var ms = new MemoryStream())
            {
                await _blobClient.DownloadToAsync(ms);
                fileContent = ms.ToArray();
            }

            Console.WriteLine($"File downloaded successfully {blobUrl}");
            Console.WriteLine($"Scanning file {blobUrl}");
            var configuration = new AMSIClientConfiguration();
            var scanResult = new Scan(configuration, 2, TimeSpan.FromSeconds(1)).Buffer(fileContent, fileContent.Length, blobUrl.Split("/").Last());
            Console.WriteLine($"File scanned succcessfully {blobUrl}");

            var resultModel = BuildResultModel(scanResult, blobUrl, blobUrl.Split("/").Last());



            Console.WriteLine($"Sending scan result message");
            await SendMessage(resultModel);

            if (!scanResult.IsSafe)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"File is infected. Scan result: {scanResult.Result}");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"Sending integration event");
                await SendIntegrationEvent(resultModel);
            }
            else
            {
                IDictionary<string, string> metadata = new Dictionary<string, string>();
                metadata.Add("BlobScanner_ScanDateTime", scanResult.TimeStamp.ToString());
                metadata.Add("BlobScanner_IsSafe", scanResult.IsSafe.ToString());
                metadata.Add("BlobScanner_ScanResult", scanResult.Result.ToString());
                metadata.Add("BlobScanner_DetectionEngine", scanResult.DetectionEngineInfo.DetectionEngine.ToString());
                await _blobClient.SetMetadataAsync(metadata);
            }


            Console.WriteLine($"Job completed successfully");

        }
        static ScanResultModel BuildResultModel(ScanResult scanResult, string fileUrl, string fileName)
        {
            var resultModel = new ScanResultModel
            {
                BlobName = fileName,
                BlobUrl = fileUrl,
                DetectionEngineInfo = scanResult.DetectionEngineInfo.DetectionEngine.ToString(),
                IsThreat = !scanResult.IsSafe,
                Result = scanResult.Result.ToString(),
                ResultDetail = scanResult.ResultDetail,
                Timestamp = scanResult.TimeStamp
            };
            return resultModel;
        }
        static async Task SendMessage(ScanResultModel resultModel)
        {
            ServiceBusMessage message = new ServiceBusMessage(JsonConvert.SerializeObject(resultModel));
            await _serviceBusSnder.SendMessageAsync(message);
        }

        static async Task SendIntegrationEvent(ScanResultModel resultModel)
        {
            EventGridEvent egEvent =
                new EventGridEvent(
                    "BlobScannerScanResult",
                    "BlobScanner.InfectedFileFound",
                    "1.0",
                    JsonConvert.SerializeObject(resultModel));
            await _eventGridclient.SendEventAsync(egEvent);
        }

        static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            _logger.LogError(ex, ex.Message, DateTimeOffset.Now);
            Console.WriteLine(e.ExceptionObject.ToString());
            Console.WriteLine("Press Enter to Exit");
            Console.ReadLine();
            Environment.Exit(0);
        }
    }
}


