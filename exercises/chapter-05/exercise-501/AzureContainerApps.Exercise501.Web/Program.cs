using AzureContainerApps.Exercise501.Web.Components;
using Azure.Storage.Blobs;
using MudBlazor.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddMudServices();

var blobConnectionString = builder.Configuration["BLOB_STORAGE_CONNECTION_STRING"]
    ?? throw new InvalidOperationException("BLOB_STORAGE_CONNECTION_STRING environment variable is not set.");

builder.Services.AddSingleton(_ => new BlobServiceClient(blobConnectionString));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();
app.MapStaticAssets();

app.MapGet("/api/quote", async (BlobServiceClient blobServiceClient) =>
{
    try
    {
        var containerClient = blobServiceClient.GetBlobContainerClient("quotes");
        var blobClient = containerClient.GetBlobClient("quote.json");

        if (!await blobClient.ExistsAsync())
        {
            return Results.Ok(new QuoteDto(
                "The best time to start was yesterday. The next best time is now.",
                "Unknown",
                DateTimeOffset.UtcNow));
        }

        var response = await blobClient.DownloadContentAsync();
        var quote = JsonSerializer.Deserialize<QuoteDto>(
            response.Value.Content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return Results.Ok(quote);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to retrieve quote: {ex.Message}");
    }
});

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(AzureContainerApps.Exercise501.Web.Client._Imports).Assembly);

app.Run();

record QuoteDto(string Quote, string Author, DateTimeOffset GeneratedAt);
