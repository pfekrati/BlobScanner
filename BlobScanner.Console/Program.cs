using System;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Newtonsoft.Json;
using System.IO;


namespace BlobScanner.ConsoleApp
{
    class Program
    {

        // name of the Service Bus topic
        static string queueName = "filesqueue";

        // the client that owns the connection and can be used to create senders and receivers
        static ServiceBusClient serviceBusClient;

        // the processor that reads and processes messages from the subscription
        static ServiceBusProcessor processor;

        static BlobClient blobClient;

        // handle received messages
        static async Task MessageHandler(ProcessMessageEventArgs args)
        {
            string body = args.Message.Body.ToString();

            var convertedBody = JsonConvert.DeserializeObject<dynamic>(body);

            string blobUrl = Convert.ToString(convertedBody.data.url);

            await GetBlobServiceClient(blobUrl);

            // complete the message. messages is deleted from the queue. 
            await args.CompleteMessageAsync(args.Message);

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
            // Create the clients that we'll use for sending and processing messages.
            serviceBusClient = new ServiceBusClient(serviceBusServer, new DefaultAzureCredential());

            // create a processor that we can use to process the messages
            processor = serviceBusClient.CreateProcessor(queueName, new ServiceBusProcessorOptions());

            // add handler to process messages
            processor.ProcessMessageAsync += MessageHandler;
            processor.ProcessErrorAsync += ProcessErrorHandler;
            
            // start processing 
            await processor.StartProcessingAsync();

            Console.ReadKey();

            // stop processing 
            await processor.StopProcessingAsync();

        }
        public static async Task GetBlobServiceClient(string blobUrl)
        {
            blobClient = new BlobClient(new Uri(blobUrl), new DefaultAzureCredential());
            
            using (var ms = new MemoryStream())
            {
                await blobClient.DownloadToAsync(ms);
                var result = ms.ToArray();
            }
        }
    }
}


