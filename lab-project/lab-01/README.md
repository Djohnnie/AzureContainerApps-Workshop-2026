[← Back to Lab Project](../README.md)

# Lab 01 — Blazor Frontend: Client-Side Snake Game

> **Build a Blazor Web App with an interactive Snake game, style it with MudBlazor, and containerise it with Docker.**

---

## Overview

In this lab you will scaffold a **Blazor Web App** using the `InteractiveWebAssembly` render mode so all game logic runs entirely in the browser. You will add the **MudBlazor** component library for a polished dark-mode UI, implement a classic Snake game engine in C#, wire up keyboard input via JavaScript interop, and finally package everything in a **Docker multi-stage image**.

By the end of the lab you will have a fully playable Snake game running at `http://localhost:8080` — either from `dotnet run` or `docker compose up`.

---

## Learning Goals

| Area | What you practice |
|------|-------------------|
| Blazor Web App | `InteractiveWebAssembly` render mode, server-side host + WASM client project |
| MudBlazor | Theming, components, layout providers |
| C# game logic | `LinkedList<T>`, `HashSet<T>`, record structs, game loop with `CancellationToken` |
| JS Interop | ES module import, `DotNetObjectReference`, keyboard capture |
| Docker | Multi-stage build, `sdk` → `aspnet` image layers |
| Docker Compose | Single-service compose file, port mapping |

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (verify: `dotnet --version`)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running
- A code editor — [VS Code](https://code.visualstudio.com/) or Visual Studio 2022+

---

## Step 1: Scaffold the Project

The `blazor` template with `--interactivity WebAssembly` creates two projects: a server host (`UltimateSnake.Frontend`) and a WASM client (`UltimateSnake.Frontend.Client`). The solution file is generated automatically.

```bash
mkdir lab-01/src && cd lab-01/src
dotnet new blazor --interactivity WebAssembly --no-https --name UltimateSnake.Frontend --output .
```

> ### 💬 Copilot Prompt
>
> ```
> Scaffold a .NET 10 Blazor Web App named UltimateSnake.Frontend in the current
> directory using the InteractiveWebAssembly render mode without HTTPS.
> ```

**What was created:**

| Project | Purpose |
|---------|---------|
| `UltimateSnake.Frontend` | ASP.NET server host — serves static files and pre-renders |
| `UltimateSnake.Frontend.Client` | Blazor WASM project — all interactive components run here |

---

## Step 2: Add MudBlazor

MudBlazor must be added to **both** projects. The server project needs it to render layout providers during SSR pre-render; the client project needs it to run components in the browser.

```bash
dotnet add UltimateSnake.Frontend/UltimateSnake.Frontend.csproj package MudBlazor
dotnet add UltimateSnake.Frontend.Client/UltimateSnake.Frontend.Client.csproj package MudBlazor
```

### Configure the server (`UltimateSnake.Frontend`)

**`Program.cs`** — register MudBlazor services:

```csharp
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddMudServices();          // ← add this
```

**`Components/App.razor`** — add MudBlazor CSS (in `<head>`) and JS (before `</body>`):

```razor
<link href="https://fonts.googleapis.com/css?family=Roboto:300,400,500,700&display=swap" rel="stylesheet" />
<link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
```

```razor
<script src="_content/MudBlazor/MudBlazor.min.js"></script>
```

**`Components/_Imports.razor`** — add `@using MudBlazor`.

**`Components/Layout/MainLayout.razor`** — replace the default sidebar layout with the MudBlazor providers and a centred dark-mode wrapper:

```razor
@inherits LayoutComponentBase

<MudThemeProvider Theme="_theme" />
<MudPopoverProvider />
<MudDialogProvider />
<MudSnackbarProvider />

<div style="min-height: 100vh; background-color: #0d1117; display: flex; flex-direction: column;
            align-items: center; justify-content: center; padding: 1rem;">
    @Body
</div>

@code {
    private readonly MudTheme _theme = new MudTheme
    {
        PaletteLight = new PaletteLight { Background = "#0d1117", Surface = "#161b22", Primary = "#4ade80" },
        PaletteDark  = new PaletteDark  { Background = "#0d1117", Surface = "#161b22", Primary = "#4ade80" }
    };
}
```

### Configure the client (`UltimateSnake.Frontend.Client`)

**`Program.cs`**:

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddMudServices();
await builder.Build().RunAsync();
```

**`_Imports.razor`** — add `@using MudBlazor` plus the game namespaces you will create shortly:

```razor
@using MudBlazor
@using UltimateSnake.Frontend.Client.Models
@using UltimateSnake.Frontend.Client.Services
```

> ### 💬 Copilot Prompt
>
> ```
> Configure MudBlazor in a Blazor Web App (InteractiveWebAssembly). Register
> AddMudServices() on both server and client, add the CSS/JS links to App.razor,
> add a dark-mode MudTheme in MainLayout, and add the required @using directives
> to both _Imports.razor files.
> ```

---

## Step 3: Implement the Snake Game

The game is split into three layers:

| Layer | File | Role |
|-------|------|------|
| Models | `Models/GameModels.cs` | `Direction`, `GamePhase`, `Position` |
| Engine | `Services/SnakeGameEngine.cs` | Pure C# game logic — no Blazor dependency |
| UI | `Components/Pages/SnakeGame.razor` | Blazor component — renders the grid, drives the loop |

All files live inside **`UltimateSnake.Frontend.Client`** so they compile to WebAssembly.

> ### 💬 Copilot Prompt
>
> ```
> In a Blazor WebAssembly project, implement a Snake game:
>
> 1. Create Models/GameModels.cs with:
>    - enum Direction { Up, Down, Left, Right }
>    - enum GamePhase { NameEntry, Countdown, Playing, GameOver }
>    - readonly record struct Position(int X, int Y)
>
> 2. Create Services/SnakeGameEngine.cs with:
>    - 30×30 wrap-around grid
>    - LinkedList<Position> for the snake body with a HashSet<Position> for O(1) collision
>    - Tick() method: remove tail first, check collision, add head, eat food
>    - SetNextDirection() ignoring 180-degree reversals
>    - SpawnFood() that avoids snake cells
>    - GetCell(x, y) returning CellType enum (Empty, SnakeHead, SnakeBody, Food)
>
> 3. Create wwwroot/js/snake.js as an ES module that listens for arrow/WASD/ZQSD keys
>    and calls dotNetRef.invokeMethodAsync('HandleKey', key)
>
> 4. Create Components/Pages/SnakeGame.razor at route "/" with InteractiveWebAssembly
>    render mode. Phases: NameEntry → Countdown (3-2-1-GO!) → Playing → GameOver.
>    Use a CSS grid for the board. Speed increases every 5 points (min 80ms/tick).
>    Support Play Again and Change Name buttons on the game-over overlay.
> ```

### Key design decisions

**Self-collision:** The tail is removed *before* checking whether the head would collide with the body. This means the head can legally move into the cell the tail just vacated — the correct classic behaviour.

**Wrap-around:** Boundaries use modular arithmetic:

```csharp
Direction.Left => new Position((head.X - 1 + GridSize) % GridSize, head.Y),
```

**Speed progression:** Every 5 points adds 5 ms of speed boost, capped at 80 ms/tick:

```csharp
_tickMs = Math.Max(80, 150 - (_engine.Score / 5) * 5);
```

**Direction conflict:** MudBlazor ships its own `Direction` enum. In `_Imports.razor` both `@using MudBlazor` and `@using UltimateSnake.Frontend.Client.Models` are present, so `HandleKey` must qualify the type:

```csharp
var dir = key switch
{
    "ArrowUp" or "w" or "W" or "z" or "Z" => Models.Direction.Up,
    ...
};
```

---

## Step 4: Run Locally

```bash
dotnet run --project UltimateSnake.Frontend/UltimateSnake.Frontend.csproj
```

Open [http://localhost:5000](http://localhost:5000) (or the URL shown in the terminal). You should see the name-entry card. Type your name, click **Start Game**, and play!

**Controls:**

| Keys | Direction |
|------|-----------|
| ↑ / W / Z | Up |
| ↓ / S | Down |
| ← / A / Q | Left |
| → / D | Right |

---

## Step 5: Add a Dockerfile

A **multi-stage Dockerfile** keeps the final image lean: the first stage uses the full SDK to build, the second stage uses only the ASP.NET runtime to serve.

> ### 💬 Copilot Prompt
>
> ```
> Write a multi-stage Dockerfile for a Blazor Web App solution with two projects:
> UltimateSnake.Frontend (server host) and UltimateSnake.Frontend.Client (WASM).
> Use mcr.microsoft.com/dotnet/sdk:10.0 for the build stage and
> mcr.microsoft.com/dotnet/aspnet:10.0 for the runtime stage.
> Expose port 8080 and set ASPNETCORE_URLS=http://+:8080.
> ```

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["UltimateSnake.Frontend/UltimateSnake.Frontend.csproj", "UltimateSnake.Frontend/"]
COPY ["UltimateSnake.Frontend.Client/UltimateSnake.Frontend.Client.csproj", "UltimateSnake.Frontend.Client/"]
RUN dotnet restore "UltimateSnake.Frontend/UltimateSnake.Frontend.csproj"

COPY . .
RUN dotnet publish "UltimateSnake.Frontend/UltimateSnake.Frontend.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UltimateSnake.Frontend.dll"]
```

| Stage | Image | Purpose |
|-------|-------|---------|
| `build` | `sdk:10.0` | Restores packages, compiles, publishes |
| `final` | `aspnet:10.0` | Lean runtime — no compiler or SDK tools |

---

## Step 6: Build and Run with Docker

Open a terminal in the `src/` folder (where `Dockerfile` lives):

```bash
docker build -t ultimate-snake-lab01-frontend:latest .
```

| Flag | Description |
|------|-------------|
| `-t ultimate-snake-lab01-frontend:latest` | Names the image |

```bash
docker run --rm -p 8080:8080 ultimate-snake-lab01-frontend:latest
```

| Flag | Description |
|------|-------------|
| `--rm` | Removes the container when stopped |
| `-p 8080:8080` | Maps host port 8080 to container port 8080 |

Open [http://localhost:8080](http://localhost:8080).

---

## Step 7: Add docker-compose.yml

Docker Compose lets you define the service once and bring it up with a single command.

```yaml
services:
  frontend:
    build:
      context: .
      dockerfile: Dockerfile
    image: ultimate-snake-lab01-frontend:latest
    ports:
      - "8080:8080"
```

| Field | Meaning |
|-------|---------|
| `build.context` | Directory Docker Compose uses as the build context |
| `image` | Name to tag the built image with |
| `ports` | `host:container` port mapping |

---

## Step 8: Run with Docker Compose

```bash
docker compose up --build
```

| Flag | Description |
|------|-------------|
| `--build` | Rebuilds the image before starting (safe to omit if nothing changed) |

Open [http://localhost:8080](http://localhost:8080) — same game, now managed by Compose.

To stop: press **Ctrl+C**, then `docker compose down`.

---

## Solution Structure

```
lab-01/
└── src/
    ├── UltimateSnake.Frontend/                  # Server host
    │   ├── Components/
    │   │   ├── App.razor                        # HTML shell + MudBlazor CSS/JS
    │   │   ├── Routes.razor
    │   │   ├── _Imports.razor
    │   │   ├── Layout/
    │   │   │   └── MainLayout.razor             # Dark MudBlazor theme
    │   │   └── Pages/
    │   │       ├── Error.razor
    │   │       └── ...
    │   └── Program.cs                           # AddMudServices + MapRazorComponents
    ├── UltimateSnake.Frontend.Client/           # WASM client
    │   ├── Components/Pages/
    │   │   ├── SnakeGame.razor                  # Game UI + loop
    │   │   └── SnakeGame.razor.css              # Scoped grid styles
    │   ├── Models/
    │   │   └── GameModels.cs
    │   ├── Services/
    │   │   └── SnakeGameEngine.cs
    │   ├── wwwroot/js/
    │   │   └── snake.js                         # Keyboard ES module
    │   ├── _Imports.razor
    │   └── Program.cs
    ├── Dockerfile
    ├── docker-compose.yml
    └── UltimateSnake.Frontend.sln
```

---

## What's Next

**[Lab 02](../lab-02/README.md)** adds a **Minimal API backend** (`UltimateSnake.Backend.Api`) that returns the server hostname. The Blazor frontend proxies calls to it via a server-side `/api/server-name` endpoint, and the Snake game displays which container it is connected to — introducing multi-container communication and service wiring via environment variables.
