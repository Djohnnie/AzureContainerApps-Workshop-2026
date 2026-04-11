using Azure.Data.Tables;
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
    var connectionString = builder.Configuration["AZURE_STORAGE_CONNECTION_STRING"]
        ?? throw new InvalidOperationException("AZURE_STORAGE_CONNECTION_STRING is not set.");
    silo.UseAzureStorageClustering(options =>
        options.TableServiceClient = new TableServiceClient(connectionString));
#endif

    silo.AddDashboard();
});

var app = builder.Build();
app.MapOrleansDashboard(routePrefix: "/dashboard");

#if ASPIRE
app.MapDefaultEndpoints();
#endif

app.Run();