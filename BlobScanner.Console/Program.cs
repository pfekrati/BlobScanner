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

namespace BlobScanner.ConsoleApp
{
    class Program
    {

        const string queueName = "filesqueue";
        const string resultsQueueName = "resultsqueue";

        static BlobClient blobClient;
        static ServiceBusClient serviceBusClient;
        static ServiceBusSender sender;


        // handle received messages
        static async Task MessageHandler(ProcessMessageEventArgs args)
        {
            try
            {
                Console.WriteLine($"New message recieved. Timestamp: {DateTime.Now}");
                string body = args.Message.Body.ToString();

                var convertedBody = JsonConvert.DeserializeObject<dynamic>(body);

                string blobUrl = Convert.ToString(convertedBody.data.url);

                Console.WriteLine($"Downloading file {blobUrl}");

                await DownloadAndScanFile(blobUrl);

                await args.CompleteMessageAsync(args.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.ToString());
                throw;
            }

        }

        static Task ProcessErrorHandler(ProcessErrorEventArgs eventArgs)
        {
            // Write details about the error to the console window
            Console.WriteLine($"an unhandled exception was encountered. This was not expected to happen.");
            Console.WriteLine(eventArgs.Exception.Message);
            return Task.CompletedTask;
        }

        static async Task Main(string[] args)
        {

            var serviceBusServer = args[0];
            serviceBusClient = new ServiceBusClient(serviceBusServer, new DefaultAzureCredential());
            sender = serviceBusClient.CreateSender(resultsQueueName);
            ServiceBusProcessor processor = serviceBusClient.CreateProcessor(queueName, new ServiceBusProcessorOptions());


            processor.ProcessMessageAsync += MessageHandler;
            processor.ProcessErrorAsync += ProcessErrorHandler;

            Console.WriteLine("Connecting to Service Bus ...");
            await processor.StartProcessingAsync();
            Console.WriteLine("Ready to receive messages ...");

            Console.ReadKey();

            await processor.StopProcessingAsync();

        }
        static async Task DownloadAndScanFile(string blobUrl)
        {
            var uri = new Uri(blobUrl);
            blobClient = new BlobClient(uri, new DefaultAzureCredential());
            byte[] fileContent;

            using (var ms = new MemoryStream())
            {
                await blobClient.DownloadToAsync(ms);
                fileContent = ms.ToArray();
            }

            Console.WriteLine($"File downloaded successfully {blobUrl}");
            Console.WriteLine($"Scanning file {blobUrl}");
            var configuration = new AMSIClientConfiguration();
            //var scanResult = new Scan(configuration, 2, TimeSpan.FromSeconds(1)).Buffer(fileContent, fileContent.Length, blobUrl.Split("/").Last());
            var scanResult = new Scan().Buffer(fileContent, fileContent.Length, blobUrl.Split("/").Last());
            Console.WriteLine($"File scanned succcessfully {blobUrl}");
        }

        static async Task SendMessage(ScanResult scanResult)
        {

            ServiceBusMessage message = new ServiceBusMessage("");
            await sender.SendMessageAsync(message);
        }
    }
}


