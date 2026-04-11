using AzureContainerApps.Exercise302.Orleans.Host.Grains;
using Azure.Data.Tables;
using Azure.Identity;
using Orleans.Configuration;

var builder = WebApplication.CreateBuilder(args);

#if ASPIRE
builder.AddServiceDefaults();
#endif

builder.UseOrleansClient(client =>
{
#if ASPIRE
    client.UseLocalhostClustering();
#else
    var tableEndpoint = builder.Configuration["AZURE_STORAGETABLE_RESOURCEENDPOINT"]
        ?? throw new InvalidOperationException("AZURE_STORAGETABLE_RESOURCEENDPOINT is not set.");
    var clientId = builder.Configuration["AZURE_STORAGETABLE_CLIENTID"]
        ?? throw new InvalidOperationException("AZURE_STORAGETABLE_CLIENTID is not set.");
    var credential = new DefaultAzureCredential(
        new DefaultAzureCredentialOptions { ManagedIdentityClientId = clientId });
    client.UseAzureStorageClustering(options =>
        options.TableServiceClient = new TableServiceClient(new Uri(tableEndpoint), credential));
#endif

    client.Configure<ClusterOptions>(options =>
    {
        options.ClusterId = "exercise-302";
        options.ServiceId = "exercise-302";
    });
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