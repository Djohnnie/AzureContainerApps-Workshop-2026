using Azure.Data.Tables;
using Orleans.Configuration;
using Orleans.Dashboard;
using UltimateSnake.Orleans.Host.Grains;

var builder = WebApplication.CreateBuilder(args);

#if ASPIRE
builder.AddServiceDefaults();
#endif

builder.UseOrleans(silo =>
{
#if ASPIRE
    silo.UseLocalhostClustering();
#else
    var connectionString = builder.Configuration["AZURE_STORAGE_CONNECTION_STRING"]
        ?? throw new InvalidOperationException("AZURE_STORAGE_CONNECTION_STRING is not set.");
    silo.UseAzureStorageClustering(options =>
        options.TableServiceClient = new TableServiceClient(connectionString));
#endif

    silo.Configure<ClusterOptions>(options =>
    {
        options.ClusterId = "lab-04";
        options.ServiceId = "lab-04";
    });

    silo.AddDashboard();
});

var app = builder.Build();
app.MapOrleansDashboard(routePrefix: "/dashboard");

#if ASPIRE
app.MapDefaultEndpoints();
#endif

app.Run();
