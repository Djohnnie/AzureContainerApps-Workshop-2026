using AzureContainerApps.Exercise301.Orleans.Host.Grains;
using Azure.Data.Tables;

var builder = WebApplication.CreateBuilder(args);

#if ASPIRE
builder.AddServiceDefaults();
#endif

builder.UseOrleansClient(client =>
{
#if ASPIRE
    client.UseLocalhostClustering();
#else
    var connectionString = builder.Configuration["AZURE_STORAGE_CONNECTION_STRING"]
        ?? throw new InvalidOperationException("AZURE_STORAGE_CONNECTION_STRING is not set.");
    client.UseAzureStorageClustering(options =>
        options.TableServiceClient = new TableServiceClient(connectionString));
#endif
});

var app = builder.Build();

#if ASPIRE
app.MapDefaultEndpoints();
#endif

app.MapGet("/status", async (IGrainFactory grains) =>
{
    var grainId = Guid.NewGuid();
    var grain = grains.GetGrain<IStatusGrain>(grainId);
    return await grain.GetStatusAsync();
});

app.Run();