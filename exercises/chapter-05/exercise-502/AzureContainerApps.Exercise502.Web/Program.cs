using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using MudBlazor.Services;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddMudServices();

var blobConnectionString = builder.Configuration["BLOB_STORAGE_CONNECTION_STRING"]
    ?? throw new InvalidOperationException("BLOB_STORAGE_CONNECTION_STRING is not configured.");
builder.Services.AddSingleton(new BlobServiceClient(blobConnectionString));

var sbConnectionString = builder.Configuration["SERVICE_BUS_CONNECTION_STRING"];
var queueName = builder.Configuration["SERVICE_BUS_QUEUE_NAME"] ?? "quote-requests";

if (!string.IsNullOrWhiteSpace(sbConnectionString))
{
    builder.Services.AddSingleton(new ServiceBusClient(sbConnectionString));
    builder.Services.AddSingleton(sp =>
        sp.GetRequiredService<ServiceBusClient>().CreateSender(queueName));
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<AzureContainerApps.Exercise502.Web.Components.App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(AzureContainerApps.Exercise502.Web.Client._Imports).Assembly);

app.MapGet("/api/quote", async (BlobServiceClient blobClient) =>
{
    try
    {
        var container = blobClient.GetBlobContainerClient("quotes");
        await container.CreateIfNotExistsAsync();
        var blob = container.GetBlobClient("quote502.json");
        if (!await blob.ExistsAsync())
            return Results.Ok(new { Quote = "The journey of a thousand miles begins with a single step.", Author = "Lao Tzu", GeneratedAt = DateTimeOffset.UtcNow });
        var download = await blob.DownloadContentAsync();
        var content = download.Value.Content.ToString();
        var quote = JsonSerializer.Deserialize<object>(content);
        return Results.Ok(quote);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/api/quote/request", async (HttpContext ctx, ServiceBusSender? sender) =>
{
    if (sender is null)
        return Results.Problem("Service Bus is not configured. Set SERVICE_BUS_CONNECTION_STRING.");

    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    using var doc = JsonDocument.Parse(body);
    var theme = doc.RootElement.TryGetProperty("theme", out var t) ? t.GetString() : null;

    if (string.IsNullOrWhiteSpace(theme))
        return Results.BadRequest(new { error = "theme is required" });

    var payload = JsonSerializer.Serialize(new { theme });
    var message = new ServiceBusMessage(Encoding.UTF8.GetBytes(payload));
    await sender.SendMessageAsync(message);
    return Results.Accepted();
});

app.Run();

namespace AzureContainerApps.Exercise502.Web.Components { }
