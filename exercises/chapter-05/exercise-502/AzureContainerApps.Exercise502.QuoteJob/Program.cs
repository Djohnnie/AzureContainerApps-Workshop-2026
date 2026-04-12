using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text;
using System.Text.Json;

// ── Configuration ───────────────────────────────────────────────────────────
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is required.");
var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
    ?? throw new InvalidOperationException("AZURE_OPENAI_API_KEY is required.");
var modelName = Environment.GetEnvironmentVariable("AZURE_OPENAI_MODEL_NAME")
    ?? throw new InvalidOperationException("AZURE_OPENAI_MODEL_NAME is required.");
var blobConnectionString = Environment.GetEnvironmentVariable("BLOB_STORAGE_CONNECTION_STRING")
    ?? throw new InvalidOperationException("BLOB_STORAGE_CONNECTION_STRING is required.");

// ── Determine theme ──────────────────────────────────────────────────────────
// Local dev: override theme via env var (Azurite has no Service Bus support)
var theme = Environment.GetEnvironmentVariable("QUOTE_THEME");

if (string.IsNullOrWhiteSpace(theme))
{
    var sbConnectionString = Environment.GetEnvironmentVariable("SERVICE_BUS_CONNECTION_STRING")
        ?? throw new InvalidOperationException("SERVICE_BUS_CONNECTION_STRING is required (or set QUOTE_THEME for local dev).");
    var queueName = Environment.GetEnvironmentVariable("SERVICE_BUS_QUEUE_NAME") ?? "quote-requests";

    await using var sbClient = new ServiceBusClient(sbConnectionString);
    var receiver = sbClient.CreateReceiver(queueName);

    Console.WriteLine($"Waiting for a message on queue '{queueName}'…");
    var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30));

    if (message is null)
    {
        Console.WriteLine("No message received within timeout. Exiting.");
        return;
    }

    var body = message.Body.ToString();
    Console.WriteLine($"Received message: {body}");

    try
    {
        using var doc = JsonDocument.Parse(body);
        theme = doc.RootElement.TryGetProperty("theme", out var t) ? t.GetString() : body;
    }
    catch
    {
        theme = body;
    }

    await receiver.CompleteMessageAsync(message);
    Console.WriteLine($"Message completed. Theme: {theme}");
}
else
{
    Console.WriteLine($"Using QUOTE_THEME override: {theme}");
}

// ── Generate quote via Semantic Kernel ────────────────────────────────────────
var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddAzureOpenAIChatCompletion(modelName, endpoint, apiKey);
var kernel = kernelBuilder.Build();

var agent = new ChatCompletionAgent
{
    Name = "QuoteAgent",
    Instructions = $$"""
        You are a creative quote generator. Generate an original, inspiring quote on the theme: "{{theme}}".
        You MUST respond with ONLY a valid JSON object — no markdown, no code fences, no extra text.
        The JSON must have exactly these fields:
        {
          "quote": "the quote text here",
          "author": "a plausible author name (real or fictional)",
          "generatedAt": "2025-01-01T00:00:00Z"
        }
        Use the current UTC time for generatedAt.
        """,
    Kernel = kernel
};

var history = new ChatHistory();
history.AddUserMessage($"Generate a quote about: {theme}");

Console.WriteLine("Calling Azure OpenAI…");

var responseText = new StringBuilder();
await foreach (var item in agent.InvokeAsync(history))
{
    responseText.Append(item.Message.Content);
}

var rawText = responseText.ToString().Trim();
Console.WriteLine($"Raw response: {rawText}");

// Strip markdown code fences if present
if (rawText.StartsWith("```"))
{
    var lines = rawText.Split('\n');
    rawText = string.Join('\n', lines.Skip(1).TakeWhile(l => !l.TrimStart().StartsWith("```"))).Trim();
}

// Build final JSON
string finalJson;
try
{
    using var doc = JsonDocument.Parse(rawText);
    var root = doc.RootElement;
    finalJson = JsonSerializer.Serialize(new
    {
        quote = root.TryGetProperty("quote", out var q) ? q.GetString() : rawText,
        author = root.TryGetProperty("author", out var a) ? a.GetString() : "Unknown",
        generatedAt = DateTimeOffset.UtcNow
    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
}
catch
{
    finalJson = JsonSerializer.Serialize(new
    {
        quote = rawText,
        author = "Azure OpenAI",
        generatedAt = DateTimeOffset.UtcNow
    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
}

// ── Upload to Blob Storage ────────────────────────────────────────────────────
var blobClient = new BlobServiceClient(blobConnectionString);
var container = blobClient.GetBlobContainerClient("quotes");
await container.CreateIfNotExistsAsync();
var blob = container.GetBlobClient("quote502.json");
using var stream = new MemoryStream(Encoding.UTF8.GetBytes(finalJson));
await blob.UploadAsync(stream, overwrite: true);

Console.WriteLine($"Quote uploaded: {finalJson}");
