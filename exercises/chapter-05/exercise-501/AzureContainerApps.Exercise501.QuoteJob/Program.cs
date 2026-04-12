using Azure.Storage.Blobs;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text;
using System.Text.Json;

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
    ?? throw new InvalidOperationException("AZURE_OPENAI_API_KEY is not set.");
var modelName = Environment.GetEnvironmentVariable("AZURE_OPENAI_MODEL_NAME")
    ?? throw new InvalidOperationException("AZURE_OPENAI_MODEL_NAME is not set.");
var blobConnectionString = Environment.GetEnvironmentVariable("BLOB_STORAGE_CONNECTION_STRING")
    ?? throw new InvalidOperationException("BLOB_STORAGE_CONNECTION_STRING is not set.");

Console.WriteLine("Quote generation job starting...");

// Build the Semantic Kernel with Azure OpenAI
var kernel = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(
        deploymentName: modelName,
        endpoint: endpoint,
        apiKey: apiKey)
    .Build();

// Create a ChatCompletionAgent using the Microsoft Semantic Kernel Agent Framework
ChatCompletionAgent agent = new()
{
    Name = "QuoteAgent",
    Instructions = """
        You are a creative writer and philosopher. Your task is to generate a single unique,
        inspirational quote of the day. The quote should be thoughtful, uplifting, and memorable.

        Respond ONLY with a valid JSON object (no markdown code fences, no extra text):
        {"quote": "Your inspirational quote here.", "author": "Author Name"}

        The author can be a real historical figure, a fictional character, or simply "Unknown".
        Never repeat previous quotes. Be creative and vary the themes and styles.
        """,
    Kernel = kernel
};

// Invoke the agent with a user prompt
ChatHistory history = [];
history.AddUserMessage("Generate a unique inspirational quote of the day. Make it creative and thought-provoking.");

var responseBuilder = new StringBuilder();
await foreach (var item in agent.InvokeAsync(history))
{
    responseBuilder.Append(item.Message.Content ?? string.Empty);
}

var rawResponse = responseBuilder.ToString().Trim();
Console.WriteLine($"Agent response: {rawResponse}");

// Strip markdown code fences if the model included them
rawResponse = rawResponse
    .Replace("```json", string.Empty)
    .Replace("```", string.Empty)
    .Trim();

// Parse the JSON response from the agent
QuoteData quoteData;
try
{
    quoteData = JsonSerializer.Deserialize<QuoteData>(rawResponse, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    }) ?? throw new InvalidOperationException("Agent returned a null response.");
}
catch (Exception ex)
{
    Console.WriteLine($"Warning: could not parse agent JSON ({ex.Message}). Using raw response as quote.");
    quoteData = new QuoteData(rawResponse, "Azure OpenAI");
}

var result = new QuoteResult(quoteData.Quote, quoteData.Author, DateTimeOffset.UtcNow);

// Write the quote to Azure Blob Storage
var blobServiceClient = new BlobServiceClient(blobConnectionString);
var containerClient = blobServiceClient.GetBlobContainerClient("quotes");
await containerClient.CreateIfNotExistsAsync();

var blobClient = containerClient.GetBlobClient("quote.json");
var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
await blobClient.UploadAsync(BinaryData.FromString(json), overwrite: true);

Console.WriteLine($"Quote updated successfully: \"{result.Quote}\" — {result.Author}");

record QuoteData(string Quote, string Author);
record QuoteResult(string Quote, string Author, DateTimeOffset GeneratedAt);