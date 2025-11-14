using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;          
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;              
using System;

namespace ABC_Retail_Functions

{
    public class Program

    {
        public static void Main(string[] args)

        {
            var builder = FunctionsApplication.CreateBuilder(args);

            builder.ConfigureFunctionsWebApplication();

            var cs = Environment.GetEnvironmentVariable("AzureWebJobsStorage")

                     ?? throw new InvalidOperationException("AzureWebJobsStorage is not configured.");

            // Register Azure Storage clients
            builder.Services.AddSingleton(new BlobServiceClient(cs));
            builder.Services.AddSingleton(new ShareServiceClient(cs));
            builder.Services.AddSingleton(new QueueServiceClient(cs));
            builder.Services.AddSingleton(new TableServiceClient(cs));

            var app = builder.Build();

            app.Run();   

        }
    }
}
