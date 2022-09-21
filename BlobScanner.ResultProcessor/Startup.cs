using System;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(BlobScanner.ResultProcessor.Startup))]

namespace BlobScanner.ResultProcessor
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var config = new ConfigurationBuilder()
                            .AddEnvironmentVariables() 
                            .Build();
            
            builder.Services.AddSingleton<ILogAnalyticsClient>(_ => new LogAnalyticsClient(config["LogAnalyticsCustomerId"], 
                                                                                           config["LogAnalyticsSharedKey"]));

            builder.Services.AddSingleton<IQuarantineClient>(_ => new QuarantineClient(new Uri(config["QuarantineContainerUrl"]),
                                                                                       config["ManagedIdentityClientId"]));

            builder.Services.Add(new ServiceDescriptor(typeof(IMetricsClient), typeof(MetricsClient), ServiceLifetime.Singleton));
        }
    }
}