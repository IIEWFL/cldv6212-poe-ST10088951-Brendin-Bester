#nullable enable
using ABCRetailFunctions.Services;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights.WorkerService;

var builder = FunctionsApplication.CreateBuilder(args);

// Configure Azure Functions web application
builder.ConfigureFunctionsWebApplication();

// Enable Application Insights telemetry
builder.Services.AddApplicationInsightsTelemetryWorkerService();

// Register storage services using factory method
builder.Services.AddSingleton(sp => CreateStorageService<TableStorageService>(sp, "Customer", "table"));
builder.Services.AddSingleton(sp => CreateStorageService<BlobStorageService>(sp, "customer-pictures", "blob"));
builder.Services.AddSingleton(sp => CreateStorageService<FileShareStorageService>(sp, "abcretail-log-files", "fileshare"));
builder.Services.AddSingleton(sp => CreateStorageService<QueueStorageService>(sp, "abcretail-log-messages", "queue"));

builder.Build().Run();


//  Factory method for storage services
static T CreateStorageService<T>(IServiceProvider sp, string serviceIdentifier, string serviceType) where T : class
{
    var logger = sp.GetRequiredService<ILogger<Program>>();
    var storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

    if (string.IsNullOrEmpty(storageConnectionString) || string.IsNullOrWhiteSpace(serviceIdentifier))
    {
        logger.LogError("Storage connection string is not configured.");
        throw new InvalidOperationException("Configuration is Invalid.");
    }

    logger.LogInformation($"Using {serviceType} identifier: {serviceIdentifier}");

    return serviceType switch
    {
        "table" => new TableStorageService(storageConnectionString, serviceIdentifier) as T,
        "blob" => new BlobStorageService(storageConnectionString, serviceIdentifier) as T,
        "queue" => new QueueStorageService(storageConnectionString, serviceIdentifier) as T,
        "fileshare" => new FileShareStorageService(storageConnectionString, serviceIdentifier) as T,
        _ => throw new NotImplementedException($"'{serviceType}' is not implemented.")
    };
}
