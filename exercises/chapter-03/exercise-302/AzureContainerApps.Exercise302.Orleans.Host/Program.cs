using Azure.Data.Tables;
using Azure.Identity;
using Orleans.Configuration;
using Orleans.Dashboard;

var builder = WebApplication.CreateBuilder(args);

#if ASPIRE
builder.AddServiceDefaults();
#endif

builder.UseOrleans(silo =>
{
#if ASPIRE
    silo.UseLocalhostClustering();
#else
    var tableEndpoint = builder.Configuration["AZURE_STORAGETABLE_RESOURCEENDPOINT"]
        ?? throw new InvalidOperationException("AZURE_STORAGETABLE_RESOURCEENDPOINT is not set.");
    var clientId = builder.Configuration["AZURE_STORAGETABLE_CLIENTID"]
        ?? throw new InvalidOperationException("AZURE_STORAGETABLE_CLIENTID is not set.");
    var credential = new DefaultAzureCredential(
        new DefaultAzureCredentialOptions { ManagedIdentityClientId = clientId });
    silo.UseAzureStorageClustering(options =>
        options.TableServiceClient = new TableServiceClient(new Uri(tableEndpoint), credential));
#endif

    silo.Configure<ClusterOptions>(options =>
    {
        options.ClusterId = "exercise-302";
        options.ServiceId = "exercise-302";
    });

    silo.AddDashboard();
});

var app = builder.Build();
app.MapOrleansDashboard(routePrefix: "/dashboard");

#if ASPIRE
app.MapDefaultEndpoints();
#endif

app.Run();