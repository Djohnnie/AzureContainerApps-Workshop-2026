using AzureContainerApps.Exercise401.Orleans.Scaler.Services;
using Azure.Data.Tables;
using Orleans.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddGrpc();

builder.Host.UseOrleansClient((hostBuilder, clientBuilder) =>
{
    var connectionString = hostBuilder.Configuration.GetValue<string>("AZURE_STORAGE_CONNECTION_STRING");

    clientBuilder.Configure<ClusterOptions>(options =>
    {
        options.ClusterId = "exercise-401";
        options.ServiceId = "exercise-401";
    });

#if DEBUG
    clientBuilder.UseLocalhostClustering();
#else
    clientBuilder.UseAzureStorageClustering(options =>
        options.TableServiceClient = new TableServiceClient(connectionString));
#endif
});

var app = builder.Build();

app.MapGrpcService<ExternalScalerService>();
app.MapGet("/", () => "This is the Orleans KEDA external scaler. Communication must be made through a gRPC client.");

app.Run();
