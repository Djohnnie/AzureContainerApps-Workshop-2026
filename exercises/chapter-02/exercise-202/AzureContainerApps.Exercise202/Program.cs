using Spectre.Console;

var builder = WebApplication.CreateBuilder(args);

var backgroundColor = builder.Configuration["BACKGROUND_COLOR"] ?? "#1e1e2e";

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

app.MapGet("/", () => Results.Content($$"""
    <!DOCTYPE html>
    <html lang="en">
    <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>Azure Container Apps — Exercise 202</title>
        <style>
            *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

            body {
                background-color: {{backgroundColor}};
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
                background-color: rgba(255,255,255,0.12);
                color: #cba6f7;
                border: 1px solid rgba(255,255,255,0.2);
                border-radius: 0.5rem;
                padding: 0.4rem 1rem;
                font-size: 0.85rem;
            }

            .color-badge {
                background-color: rgba(255,255,255,0.1);
                color: #f9e2af;
                border: 1px solid rgba(255,255,255,0.2);
                border-radius: 0.5rem;
                padding: 0.4rem 1rem;
                font-size: 0.85rem;
            }
        </style>
    </head>
    <body>
        <pre>{{asciiArt}}</pre>
        <p class="subtitle">Azure Container Apps Workshop</p>
        <span class="badge">Exercise 202 — Multi-Revision Container App</span>
        <span class="color-badge">🎨 Background: {{backgroundColor}}</span>
    </body>
    </html>
    """, "text/html"));

app.Run();
