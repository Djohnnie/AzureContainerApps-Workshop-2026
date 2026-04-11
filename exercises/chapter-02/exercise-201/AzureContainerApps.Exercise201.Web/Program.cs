using Spectre.Console;

var builder = WebApplication.CreateBuilder(args);

var apiBaseUrl = builder.Configuration["API_BASE_URL"] ?? "http://localhost:8081";

builder.Services.AddHttpClient("api", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

var app = builder.Build();

var sw = new StringWriter();
var spectreConsole = AnsiConsole.Create(new AnsiConsoleSettings
{
    Ansi = AnsiSupport.No,
    ColorSystem = ColorSystemSupport.NoColors,
    Out = new AnsiConsoleOutput(sw),
});
spectreConsole.Write(new FigletText("Hello, World!"));

var asciiArt = System.Net.WebUtility.HtmlEncode(sw.ToString());

app.MapGet("/", async (IHttpClientFactory httpClientFactory) =>
{
    string serverName;
    try
    {
        var client = httpClientFactory.CreateClient("api");
        var response = await client.GetFromJsonAsync<ServerNameResponse>("/server-name");
        serverName = response?.ServerName ?? "unknown";
    }
    catch
    {
        serverName = "unavailable";
    }

    return Results.Content($$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>Azure Container Apps — Exercise 201</title>
            <style>
                *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

                body {
                    background-color: #1e1e2e;
                    color: #cdd6f4;
                    font-family: 'Courier New', Courier, monospace;
                    min-height: 100vh;
                    display: flex;
                    flex-direction: column;
                    align-items: center;
                    justify-content: center;
                    gap: 1.5rem;
                    padding: 2rem;
                }

                pre {
                    color: #89dceb;
                    font-size: clamp(0.55rem, 1.8vw, 1rem);
                    line-height: 1.2;
                    white-space: pre;
                }

                .subtitle {
                    color: #a6e3a1;
                    font-size: 1rem;
                    letter-spacing: 0.05em;
                }

                .badge {
                    background-color: #313244;
                    color: #cba6f7;
                    border: 1px solid #45475a;
                    border-radius: 0.5rem;
                    padding: 0.4rem 1rem;
                    font-size: 0.85rem;
                }

                .api-info {
                    background-color: #313244;
                    border: 1px solid #45475a;
                    border-radius: 0.5rem;
                    padding: 0.75rem 1.25rem;
                    font-size: 0.9rem;
                    color: #f9e2af;
                }
            </style>
        </head>
        <body>
            <pre>{{asciiArt}}</pre>
            <p class="subtitle">Azure Container Apps Workshop</p>
            <span class="badge">Exercise 201 — Multi-Container App</span>
            <div class="api-info">
                Connected to server '{{serverName}}'
            </div>
        </body>
        </html>
        """, "text/html");
});

app.Run();

record ServerNameResponse(string ServerName);
