[← Back to Lab Project](../README.md)

# Lab 02 — Backend API: Server Discovery

> **Add a minimal API backend to the Snake game and connect it to the Blazor frontend — demonstrating multi-container communication and environment-variable-based service wiring.**

---

## Overview

In this lab you extend the Ultimate Snake application from Lab 01 by adding a dedicated **backend API** container. The API exposes a single endpoint that returns its server hostname. The Blazor frontend calls this endpoint through a **server-side proxy** and displays which backend container it is talking to — a live illustration of service-to-service communication inside a multi-container application.

By the end of the lab you will have two containers running together via Docker Compose: the Blazor frontend (port 8080) and the minimal API backend (internal only, no public port), with the frontend wiring them together using an environment variable.

---

## Learning Goals

| Area | What you practice |
|------|-------------------|
| Minimal API | Single-endpoint ASP.NET API, `Environment.MachineName` |
| Service wiring | `API_BASE_URL` environment variable, named `HttpClient` |
| Server-side proxy | Frontend proxies `/api/server-name` → backend `/server-name` |
| Blazor WASM + API | `HttpClient` in WASM calling same-origin proxy endpoint |
| Docker Compose | Multi-service compose, internal network, healthcheck, `depends_on` |

---

## Prerequisites

- Completed [Lab 01](../lab-01/README.md) (or use the provided starting code)
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (verify: `dotnet --version`)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running
- A code editor — [VS Code](https://code.visualstudio.com/) or Visual Studio 2022+

---

## Architecture

```
Browser
  │
  ▼
┌─────────────────────────────────┐
│  UltimateSnake.Frontend         │  :8080 (public)
│  (Blazor Server Host)           │
│                                 │
│  GET /api/server-name ──────────┼──► UltimateSnake.Backend.Api
│  (server-side proxy)            │        GET /server-name
└─────────────────────────────────┘        returns MachineName
          ▲
          │  WASM calls same-origin
┌─────────────────────────────────┐
│  UltimateSnake.Frontend.Client  │
│  (Blazor WebAssembly)           │
│  displays: 🖥 Server: <name>    │
└─────────────────────────────────┘
```

The WASM code runs in the browser and calls `/api/server-name` on its own origin. The **frontend server** receives that request and proxies it to the backend API using the `API_BASE_URL` environment variable. This keeps the backend URL server-side only — the browser never sees it directly.

---

## Step 1: Create the Backend API Project

Create a new minimal API project alongside the frontend projects.

```bash
mkdir src/UltimateSnake.Backend.Api && cd src/UltimateSnake.Backend.Api
dotnet new webapi --no-openapi --name UltimateSnake.Backend.Api --output .
```

> ### 💬 Copilot Prompt
>
> ```
> Create a .NET 10 minimal API project called UltimateSnake.Backend.Api.
> Add a single GET /server-name endpoint that returns a JSON object
> { "serverName": "<machine name>" } using Environment.MachineName.
> No OpenAPI, no HTTPS.
> ```

**`Program.cs`:**

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/server-name", () => new { ServerName = Environment.MachineName });

app.Run();
```

Add a Dockerfile for the API:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["UltimateSnake.Backend.Api.csproj", "."]
RUN dotnet restore "UltimateSnake.Backend.Api.csproj"

COPY . .
RUN dotnet publish "UltimateSnake.Backend.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UltimateSnake.Backend.Api.dll"]
```

Add the new project to the solution:

```bash
dotnet sln add UltimateSnake.Backend.Api/UltimateSnake.Backend.Api.csproj
```

---

## Step 2: Add a Proxy Endpoint to the Frontend Server

The Blazor frontend server needs to:

1. Read `API_BASE_URL` from configuration (set via environment variable at runtime)
2. Register a named `HttpClient` pointing to the backend
3. Expose a proxy endpoint `/api/server-name` that the WASM client can call

> ### 💬 Copilot Prompt
>
> ```
> In the UltimateSnake.Frontend Program.cs, read an API_BASE_URL config value
> (default "http://localhost:8081"), register a named HttpClient "backend" pointing
> to that URL, and add a GET /api/server-name endpoint that proxies the request to
> the backend's /server-name endpoint. Return the result as-is, or return
> { "serverName": "unavailable" } if the backend is unreachable.
> ```

**`UltimateSnake.Frontend/Program.cs`** — add before `var app = builder.Build()`:

```csharp
var apiBaseUrl = builder.Configuration["API_BASE_URL"] ?? "http://localhost:8081";
builder.Services.AddHttpClient("backend", c => c.BaseAddress = new Uri(apiBaseUrl));
```

Add the proxy endpoint after `app.UseAntiforgery()`:

```csharp
app.MapGet("/api/server-name", async (IHttpClientFactory factory) =>
{
    try
    {
        var client = factory.CreateClient("backend");
        var result = await client.GetFromJsonAsync<ServerNameResponse>("/server-name");
        return Results.Ok(result);
    }
    catch
    {
        return Results.Ok(new ServerNameResponse("unavailable"));
    }
});
```

Add the record type at the bottom of `Program.cs`:

```csharp
record ServerNameResponse(string ServerName);
```

---

## Step 3: Register HttpClient in the Blazor WASM Client

The WASM client runs in the browser and needs an `HttpClient` to call `/api/server-name` on its own origin.

> ### 💬 Copilot Prompt
>
> ```
> In the Blazor WASM Program.cs, register a scoped HttpClient using
> builder.HostEnvironment.BaseAddress as the base address so the WASM code
> can call same-origin API endpoints without hardcoding a URL.
> ```

**`UltimateSnake.Frontend.Client/Program.cs`** — add the `HttpClient` registration:

```csharp
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});
```

---

## Step 4: Display the Server Name in the Snake Game

Modify `SnakeGame.razor` to inject `HttpClient`, fetch the server name on initialisation, and show it in the info bar.

> ### 💬 Copilot Prompt
>
> ```
> In SnakeGame.razor, inject HttpClient and add OnInitializedAsync to call
> GET /api/server-name, storing the result in a _serverName field (default "…",
> fallback "unavailable"). Display "🖥 Server: <name>" in the info bar alongside
> the score and speed indicators.
> ```

Add the injection at the top:

```razor
@inject HttpClient Http
```

Add the field in `@code`:

```csharp
private string _serverName = "…";
```

Add `OnInitializedAsync`:

```csharp
protected override async Task OnInitializedAsync()
{
    try
    {
        var result = await Http.GetFromJsonAsync<ServerNameResponse>("/api/server-name");
        _serverName = result?.ServerName ?? "unknown";
    }
    catch
    {
        _serverName = "unavailable";
    }
}

private record ServerNameResponse(string ServerName);
```

Add the server name to the info bar in the template:

```razor
<span style="color:#8b949e;">🖥 Server: <strong>@_serverName</strong></span>
```

---

## Step 5: Add docker-compose.yml

> ### 💬 Copilot Prompt
>
> ```
> Write a docker-compose.yml for two services:
> - api: built from ./UltimateSnake.Backend.Api, image ultimate-snake-lab02-api:latest,
>   internal only (no ports), with a TCP healthcheck on port 8080
> - frontend: built from . (src/ context), image ultimate-snake-lab02-frontend:latest,
>   ports 8080:8080, API_BASE_URL=http://api:8080, depends_on api with service_healthy.
> Both on a shared network called ultimate-snake.
> ```

```yaml
services:
  api:
    build:
      context: ./UltimateSnake.Backend.Api
      dockerfile: Dockerfile
    image: ultimate-snake-lab02-api:latest
    healthcheck:
      test: ["CMD-SHELL", "bash -c 'echo > /dev/tcp/localhost/8080'"]
      interval: 5s
      timeout: 3s
      retries: 5
    networks:
      - ultimate-snake

  frontend:
    build:
      context: .
      dockerfile: Dockerfile
    image: ultimate-snake-lab02-frontend:latest
    ports:
      - "8080:8080"
    environment:
      - API_BASE_URL=http://api:8080
    depends_on:
      api:
        condition: service_healthy
    networks:
      - ultimate-snake

networks:
  ultimate-snake:
```

| Key | Why |
|-----|-----|
| No `ports` on `api` | The backend is only reachable inside the Docker network — not from the host |
| `API_BASE_URL=http://api:8080` | Docker's internal DNS resolves the service name `api` to the container |
| `healthcheck` | Ensures the frontend only starts after the API is ready to accept connections |
| `depends_on: condition: service_healthy` | Waits for the healthcheck to pass before starting the frontend |

---

## Step 6: Run Locally with dotnet run

Start the API on port 8081 in one terminal:

```bash
dotnet run --project src/UltimateSnake.Backend.Api/UltimateSnake.Backend.Api.csproj --urls http://localhost:8081
```

Start the frontend in a second terminal:

```bash
API_BASE_URL=http://localhost:8081 dotnet run --project src/UltimateSnake.Frontend/UltimateSnake.Frontend.csproj
```

Open [http://localhost:5000](http://localhost:5000). The Snake game loads; once you pass the name entry you will see **🖥 Server: &lt;your-machine-name&gt;** in the info bar.

---

## Step 7: Build and Run with Docker Compose

Open a terminal in the `src/` folder and run:

```bash
docker compose up --build
```

| Flag | Description |
|------|-------------|
| `--build` | Rebuilds both images before starting |

Open [http://localhost:8080](http://localhost:8080).

The server name in the info bar is now the **container ID** of the API container — this changes every time you recreate the container, which sets the stage for showing **replica scaling** in the next lab step.

To stop:

```bash
docker compose down
```

---

## Solution Structure

```
lab-02/
└── src/
    ├── UltimateSnake.Frontend/              # Server host — proxy endpoint + Blazor host
    │   ├── Components/
    │   │   ├── App.razor
    │   │   ├── Routes.razor
    │   │   ├── _Imports.razor
    │   │   ├── Layout/
    │   │   │   └── MainLayout.razor
    │   │   └── Pages/
    │   │       ├── Error.razor
    │   │       └── NotFound.razor
    │   └── Program.cs                       # Added: HttpClient + /api/server-name proxy
    ├── UltimateSnake.Frontend.Client/       # WASM client — fetches and displays server name
    │   ├── Components/Pages/
    │   │   ├── SnakeGame.razor              # Modified: OnInitializedAsync + server name display
    │   │   └── SnakeGame.razor.css
    │   ├── Models/GameModels.cs
    │   ├── Services/SnakeGameEngine.cs
    │   ├── wwwroot/js/snake.js
    │   └── Program.cs                       # Added: HttpClient registration
    ├── UltimateSnake.Backend.Api/           # NEW: minimal API
    │   ├── Program.cs                       # GET /server-name → MachineName
    │   ├── UltimateSnake.Backend.Api.csproj
    │   └── Dockerfile
    ├── Dockerfile                           # Frontend multi-stage build
    ├── docker-compose.yml                   # Two-service compose
    └── UltimateSnake.slnx
```

---

## What's Next

**Lab 03** will deploy both containers to **Azure Container Apps**, configure the API with **internal ingress** (service-to-service only), and use an **environment variable** on the frontend Container App to wire the two services together — exactly the same pattern as this lab, now running in Azure.
