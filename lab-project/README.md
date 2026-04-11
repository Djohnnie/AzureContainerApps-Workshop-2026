[← Back to Workshop](../README.md)

# 🐍 Ultimate Snake — Integrated Lab Project

> **The capstone project for the Azure Container Apps Workshop.**
> Participants build a real-time multiplayer Snake game from scratch, progressively deploying it as a distributed, cloud-native application on Azure Container Apps.

---

## 🧪 Labs

| Lab | Title | What you build |
|-----|-------|----------------|
| [Lab 01](lab-01/README.md) | Blazor Frontend | Scaffold the Blazor Snake game, add MudBlazor, containerise with Docker |
| [Lab 02](lab-02/README.md) | Backend API | Add a minimal API backend, wire it to the frontend, run both with Docker Compose |

---

## 📋 Table of Contents

- [Overview](#overview)
- [Game Design](#-game-design)
  - [The Snake Game](#the-snake-game)
  - [Multiplayer Modes](#multiplayer-modes)
  - [Controls](#controls)
- [Architecture](#-architecture)
  - [Container Overview](#container-overview)
  - [Container 1 — Blazor Frontend](#container-1--blazor-frontend)
  - [Container 2 — Game API](#container-2--game-api)
  - [Container 3 — Orleans Cluster](#container-3--orleans-cluster)
  - [Container 4 — Game Tick Service](#container-4--game-tick-service)
  - [Container 5 — KEDA External Scaler](#container-5--keda-external-scaler)
  - [Azure Infrastructure](#azure-infrastructure)
- [Orleans Grain Design](#-orleans-grain-design)
- [Real-Time Communication](#-real-time-communication)
- [Custom KEDA External Scaler](#-custom-keda-external-scaler)
  - [Why a Custom Scaler?](#why-a-custom-scaler)
  - [The KEDA External Scaler Protocol](#the-keda-external-scaler-protocol)
  - [Querying Orleans via IManagementGrain](#querying-orleans-via-imanagementgrain)
  - [gRPC Service Implementation](#grpc-service-implementation)
  - [Deployment and ACA Scale Rule](#deployment-and-aca-scale-rule)
  - [Advanced: Proactive Scaling with StreamIsActive](#advanced-proactive-scaling-with-streamIsActive)
- [Extension Ideas](#-extension-ideas)
  - [Redis Cache](#-redis-cache)
  - [Azure Container Jobs](#-azure-container-jobs)
  - [Dapr Integration](#-dapr-integration)
  - [Leaderboards](#-leaderboards)
- [Solution Structure](#-solution-structure)
- [Workshop Journey](#-workshop-journey)

---

## Overview

**Ultimate Snake** is the integrated, end-to-end lab project that ties together every concept taught in this workshop. Participants do not just deploy pre-built containers — they build the entire application incrementally, adding Azure Container Apps features at each stage.

By the end of the lab, participants will have built and deployed:

| # | Component | Technology | Azure Container Apps Feature |
|---|-----------|------------|------------------------------|
| 1 | Game frontend | Blazor WebAssembly (hosted) + MudBlazor | Ingress, environment variables |
| 2 | Game API + SignalR Hub | ASP.NET Minimal API | Service-to-service discovery |
| 3 | Orleans cluster | Microsoft Orleans 9 | Scale rules, multiple replicas |
| 4 | Game tick service | .NET Worker Service | Dapr, pub/sub |
| 5 | KEDA external scaler | ASP.NET gRPC Service | Custom KEDA scaler (external) |
| 6 | Leaderboard job | .NET Console / Worker | Azure Container Jobs |
| — | State store | Azure Blob + Table Storage | Managed Identity |
| — | Cache + backplane | Azure Cache for Redis | |
| — | Messaging | Azure Service Bus | |

---

## 🎮 Game Design

### The Snake Game

Ultimate Snake is based on the classic Snake mechanic, played on a **square grid of tiles**. The rules are intentionally simple so that the focus stays on the cloud architecture, not on complex game logic.

**Grid & movement:**
- The playing field is a square grid (e.g., 30 × 30 tiles). The size is configurable per room.
- Every **game tick**, each snake moves one tile in its current direction.
- Snakes **wrap around** — moving off the right edge reappears on the left, moving off the top reappears at the bottom, and vice versa. There is no game-over from hitting a wall.
- A snake starts with **4 body tiles**, horizontally centered in the grid, facing right.

**Food:**
- One piece of food exists on the grid at any time. When eaten, the snake grows by one tile and new food spawns at a random unoccupied tile.
- Eating food increases the player's score.

**Snake growth:**
- The snake grows by deferring tail removal: when food is eaten, the next tick does not remove the last tail tile, effectively extending the snake by one.

**Game-over conditions:**
- A snake collides with its own body (always a loss).
- In **Versus mode**, a snake collides with another snake's body (the colliding snake dies).

---

### Multiplayer Modes

Games are organised into **rooms**. A room has a unique, human-readable **room code** (e.g., `TIGER-4821`) generated when the host creates it. Other players join by entering this code.

#### 🤝 Transparent Mode (up to 4 players)

- All snakes are visible with distinct colours, but they **pass through each other** — no collision death between players.
- Each player competes independently for the highest score (most food eaten).
- Great for casual, friendly play and as the introductory multiplayer mode for participants to implement first.

#### ⚔️ Versus Mode (up to 2 players)

- Only **one piece of food** exists at a time — players race to eat it first.
- Snakes can **kill each other**: running head-first into any tile of an opponent's body causes the colliding snake to die.
- Head-on collisions (both snakes enter the same tile on the same tick) kill both snakes simultaneously.
- The surviving snake wins; if both die simultaneously, it is a draw.
- This mode introduces the need for deterministic, server-authoritative game state (which Orleans handles cleanly).

#### Room lifecycle

```
[Host creates room] → [Room code displayed] → [Players join with code]
        ↓
[Each player clicks "Ready"] → [Host sees all players ready]
        ↓
[Host presses "Start"] → [Countdown (3-2-1)] → [Game running]
        ↓
[Game ends (all snakes dead / time limit)] → [Scores shown] → [Room closed or replay]
```

---

### Controls

| Key | Action |
|-----|--------|
| `↑` / `W` / `Z` | Move up |
| `↓` / `S` | Move down |
| `←` / `A` / `Q` | Move left |
| `→` / `D` | Move right |

- **QWERTY and AZERTY** keyboard layouts are both supported. Players can switch their layout in a settings panel (swaps `W`/`Z` and `A`/`Q`).
- Direction changes are **buffered**: a player can queue one direction change that will take effect on the next tick, preventing missed inputs on fast connections.
- A snake **cannot reverse direction** — pressing the opposite of the current direction is ignored.

---

## 🏗️ Architecture

### Container Overview

```
┌──────────────────────────────────────────────────────────────────────────┐
│                      Azure Container Apps Environment                    │
│                                                                          │
│  ┌────────────┐    ┌─────────────────┐    ┌──────────────────────────┐  │
│  │Container 1 │    │   Container 2   │    │      Container 3         │  │
│  │            │───▶│                 │───▶│                          │  │
│  │   Blazor   │    │  Minimal API    │    │  Orleans Cluster         │  │
│  │   Server   │◀───│  + SignalR Hub  │    │  (Silo Host)        ▲   │  │
│  └────────────┘    └───────┬─────────┘    └─────────────────────┼───┘  │
│         ▲                  │ pub/sub                Orleans client│      │
│         │           ┌──────▼──────────┐                         │      │
│         └───────────│   Container 4   │─────────────────────────┘      │
│          SignalR     │  Game Tick Svc  │                                │
│                      └─────────────────┘                               │
│                                                                          │
│  ┌──────────────────────────────────────┐                               │
│  │           Container 5                │                               │
│  │   KEDA External Scaler (gRPC)        │──▶ IManagementGrain query    │
│  │   • active GameGrain count per silo  │    to Container 3             │
│  │   • silo overload signal             │                               │
│  └──────────────────────────────────────┘                               │
│         ▲                    │                                           │
│   KEDA polls /GetMetrics     └──▶ KEDA adjusts Container 3 replicas     │
│                                                                          │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │                         Azure Services                           │   │
│  │  Redis · Blob Storage · Table Storage · Service Bus · Key Vault  │   │
│  └──────────────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────────┘
```

---

### Container 1 — Blazor Frontend

**Technology:** .NET 10, Blazor WebAssembly (hosted), MudBlazor

The frontend is a **Blazor WebAssembly (hosted)** application, meaning the UI logic runs in the browser via WebAssembly and is served by an ASP.NET host process. The browser makes HTTP and SignalR calls directly to the backend containers. This puts rendering on the client and reduces server-side connection overhead.

**Key pages and components:**

| Page / Component | Purpose |
|-----------------|---------|
| `/` — Home | Enter player name, choose to create or join a room |
| `/room/{code}` — Lobby | Show connected players, ready status, settings |
| `/game/{code}` — Game | The live game canvas rendered with `MudBlazor` grid or custom HTML Canvas via JS interop |
| `/leaderboard` — Leaderboard | All-time and current-session scores |
| `<SnakeCanvas>` | Blazor component that renders the game grid and handles keyboard input |
| `<PlayerList>` | Lobby component showing who is connected and their ready state |

**How Blazor connects:**
- On game start, the Blazor page establishes a **SignalR connection** to Container 4 (the Game Tick Service), subscribing to the room's game state stream.
- REST calls to Container 2 (API) handle room management (create, join, ready, start).

> **Workshop note:** Participants start with a working Blazor scaffold and progressively wire it up to the backend containers. The game canvas is initially a stub that renders hardcoded state before real state arrives.

---

### Container 2 — Game API

**Technology:** .NET 10, ASP.NET Minimal API, SignalR

The API is the gateway between the frontend and the Orleans cluster. It exposes a clean HTTP surface and also hosts the **SignalR Hub** that pushes real-time game events to Blazor clients.

**Minimal API endpoints:**

```
POST   /rooms                      → Create a new room, returns room code
GET    /rooms/{code}               → Get room info (players, mode, status)
POST   /rooms/{code}/join          → Join a room with player name
POST   /rooms/{code}/ready         → Mark the calling player as ready
POST   /rooms/{code}/start         → Host starts the game (validates all ready)
DELETE /rooms/{code}               → Host closes the room
POST   /rooms/{code}/direction     → Submit a direction change for current tick
GET    /leaderboard                → Get paginated all-time leaderboard
GET    /leaderboard/room/{code}    → Get current room's session leaderboard
```

**SignalR Hub (`/hubs/game`):**
- Clients connect to this hub from the Blazor frontend.
- When Container 4 (Game Tick Service) advances the game state, it publishes game state to **Redis Pub/Sub**. The API subscribes to Redis and broadcasts the state to the correct SignalR group (keyed by room code).
- This means multiple API replicas can all push to their respective connected clients without missing messages — Redis acts as the **backplane**.

**Orleans client:**
- The API hosts an **Orleans client** that connects to the Orleans cluster (Container 3) for all game logic calls.
- No game state lives in the API itself — it is stateless and can scale horizontally.

---

### Container 3 — Orleans Cluster

**Technology:** .NET 10, Microsoft Orleans 9, Azure Blob Storage, Azure Table Storage

The Orleans cluster is the **authoritative game state** engine. All game logic — snake movement validation, collision detection, food placement, score tracking — runs inside Orleans grains. This makes the system reliable, consistent, and naturally distributed.

**Clustering configuration:**
- **Azure Table Storage** is used for Orleans cluster membership (silo discovery). Each Container 3 replica registers itself in the membership table and discovers peers.
- Multiple replicas can run simultaneously; Orleans handles silo failure and grain reactivation automatically.
- In the Azure Container Apps environment, the minimum replica count is set to **2** to demonstrate resilience.

**Grain state persistence:**
- **Azure Blob Storage** is used as the Orleans grain state store (via the `UseAzureBlobGrainStorage` provider).
- State is serialised to JSON and stored in a dedicated blob container (`orleans-grain-state`).
- This means game state survives silo restarts — an important demonstration of durable state in ACA.

**Authentication:**
- Both Azure Blob Storage and Azure Table Storage are accessed using **Managed Identity** (no connection strings in code or environment variables).

See [Orleans Grain Design](#-orleans-grain-design) for the full grain breakdown.

---

### Container 4 — Game Tick Service

**Technology:** .NET 10, Worker Service, Orleans Client, Redis, SignalR

The Game Tick Service is the **heartbeat** of the game. It is a headless `.NET Worker Service` with no HTTP surface — it runs a tight background loop and advances every active game on every tick.

**How it works:**

1. The service maintains an in-memory list of **active room codes** (sourced from a Redis `SET` that the API updates when games start/stop).
2. On every tick interval (default: **150 ms** — configurable per room for difficulty scaling), the worker iterates over active rooms.
3. For each active room, it calls `IGameGrain.TickAsync()` on the Orleans cluster via an Orleans client.
4. Orleans advances all snakes, checks collisions, spawns food if needed, and returns the new `GameState` snapshot.
5. The worker **publishes** the serialised `GameState` to a Redis Pub/Sub channel keyed by room code: `game:tick:{roomCode}`.
6. Container 2 (API) subscribes to these channels and forwards game state to Blazor clients via SignalR groups.

**Why a separate container?**
- Decoupling the tick loop from the API allows **independent scaling** — you may want 1 tick service and 5 API replicas.
- The tick service does not handle HTTP traffic; it is a pure worker. This separation is a clean demonstration of the **single-responsibility principle** in a containerised architecture.
- It also makes it easy to introduce **Dapr Pub/Sub** as a drop-in replacement for the Redis publish step (see [Dapr Integration](#-dapr-integration)).

**Scale consideration:**
- In the workshop, the tick service runs as a single replica to avoid duplicate ticks.
- The extension exercise shows how to use **distributed locks** (via Redis `SETNX`) to safely run multiple tick service replicas if needed.

---

### Container 5 — KEDA External Scaler

**Technology:** .NET 10, ASP.NET gRPC Service, Orleans Client

Azure Container Apps uses [KEDA](https://keda.sh/) under the hood for all scaling decisions. Built-in KEDA scalers cover HTTP request rate, CPU, memory, queue depth, and many more — but none of them understand **Orleans grain activation count**. Container 5 is a custom KEDA External Scaler that bridges that gap: it speaks the KEDA External Scaler gRPC protocol on one side and queries the Orleans `IManagementGrain` on the other.

**Role in the system:**
- KEDA polls this service on a configurable interval (e.g., every 15 seconds).
- The scaler queries Orleans for the count of active `GameGrain` activations across all silos.
- It returns this count as a KEDA metric; KEDA divides it by the configured `targetGrainsPerReplica` to compute the desired silo replica count.
- The result directly drives Container 3's replica count — no manual intervention needed.

**Ingress:** Internal only, gRPC port **9090**. Never exposed publicly.

See [Custom KEDA External Scaler](#-custom-keda-external-scaler) for the full implementation guide.

---

### Azure Infrastructure

| Resource | Purpose | Access method |
|----------|---------|---------------|
| **Azure Container Apps Environment** | Hosts all 4 containers + jobs in a shared virtual network | — |
| **Azure Container Registry** | Stores container images built during exercises | Managed Identity |
| **Azure Blob Storage** | Orleans grain state persistence | Managed Identity |
| **Azure Table Storage** | Orleans cluster membership | Managed Identity |
| **Azure Cache for Redis** | SignalR backplane, leaderboard cache, tick channel pub/sub | Connection string (secret ref) |
| **Azure Service Bus** | Game-over events triggering Container Jobs | Managed Identity |
| **Azure SignalR Service** | Horizontal SignalR scaling (optional, replaces Redis backplane) | Connection string |
| **Azure Key Vault** | Stores Redis + SignalR connection strings (secret refs in ACA) | Managed Identity |

---

## 🌾 Orleans Grain Design

Orleans grains are the core abstraction. Each grain is identified by a **string key** (the unique identifier shown below).

### `IPlayerGrain` — key: `playerId` (GUID)

Represents a connected player. Activated when a player joins for the first time; deactivated when they disconnect.

```csharp
public interface IPlayerGrain : IGrainWithStringKey
{
    Task<PlayerInfo> GetInfoAsync();
    Task SetNameAsync(string name);
    Task<string?> GetCurrentRoomCodeAsync();
    Task JoinRoomAsync(string roomCode);
    Task LeaveRoomAsync();
}

public record PlayerInfo(string PlayerId, string Name, string? RoomCode);
```

**State:** `PlayerState { Name, RoomCode, JoinedAt }`

---

### `IGameGrain` — key: `roomCode` (e.g., `TIGER-4821`)

Represents a game room. This is the central grain — it coordinates players, snakes, food, and the game lifecycle.

```csharp
public interface IGameGrain : IGrainWithStringKey
{
    Task<RoomInfo> CreateAsync(string hostPlayerId, GameMode mode, int gridSize);
    Task<JoinResult> JoinAsync(string playerId);
    Task SetReadyAsync(string playerId, bool ready);
    Task StartAsync(string hostPlayerId);
    Task<GameState> TickAsync();                 // Called by Game Tick Service
    Task SubmitDirectionAsync(string playerId, Direction direction);
    Task<GameState> GetStateAsync();
    Task<bool> IsActiveAsync();
}

public enum GameMode { Transparent, Versus }
public enum Direction { Up, Down, Left, Right }

public record GameState(
    string RoomCode,
    GameMode Mode,
    int GridSize,
    IReadOnlyList<SnakeState> Snakes,
    Position FoodPosition,
    IReadOnlyDictionary<string, int> Scores,
    bool IsRunning,
    int TickNumber
);
```

**State:** `GameRoomState { Mode, GridSize, Players, HostPlayerId, IsRunning, TickNumber, FoodPosition, Scores }`

**Key logic inside `TickAsync()`:**
1. For each living snake, apply its queued direction change (if valid — no reversals).
2. Move each snake's head one tile in its current direction (with wrap-around).
3. Check if the new head position collides with the snake's own body → kill that snake.
4. In Versus mode: check if head collides with any other snake's body → kill the colliding snake.
5. Check if the new head position matches the food tile → grow snake, update score, spawn new food.
6. Remove dead snakes from active play.
7. Check win/end conditions.
8. Persist updated state.
9. Return the full `GameState` snapshot.

---

### `ISnakeGrain` — key: `"{roomCode}:{playerId}"`

Represents an individual snake in a game. The `IGameGrain` manages the collection; `ISnakeGrain` holds the detailed body position list and movement logic.

```csharp
public interface ISnakeGrain : IGrainWithStringKey
{
    Task InitialiseAsync(Position startHead, Direction startDirection, int initialLength);
    Task<SnakeState> GetStateAsync();
    Task QueueDirectionAsync(Direction direction);
    Task<MoveResult> MoveAsync(Position foodPosition, bool versusMode, IReadOnlyList<SnakeState> otherSnakes);
    Task KillAsync();
    Task<bool> IsAliveAsync();
}

public record SnakeState(
    string PlayerId,
    string PlayerName,
    string Color,
    IReadOnlyList<Position> Body,       // Body[0] = head
    Direction CurrentDirection,
    bool IsAlive
);

public record Position(int X, int Y);

public record MoveResult(bool AteFood, bool Died, SnakeState NewState);
```

**State:** `SnakeGrainState { Body, Direction, QueuedDirection, IsAlive, Color }`

---

### Grain activation summary

```
Player joins room  →  IPlayerGrain activated (or fetched from state)
                   →  IGameGrain.JoinAsync() called
                   →  ISnakeGrain initialised for this player in this room

Game tick fires    →  IGameGrain.TickAsync() orchestrates all ISnakeGrains
                   →  GameState snapshot returned and published
```

---

## 📡 Real-Time Communication

### Flow diagram

```
[Browser keyboard input]
         │
         ▼
[Blazor WebAssembly (C1)] ──POST /direction──▶ [API (C2)] ──▶ [IGameGrain.QueueDirection]
                                                                   │
                                                            (queued until tick)
                                                                   │
[Game Tick Service (C4)] ──Orleans client──▶ [IGameGrain.TickAsync()]
         │                                           │
         │                                    returns GameState
         │
         ▼
[Redis Pub/Sub: "game:tick:{roomCode}"]
         │
         ▼
[API (C2) Redis subscriber]
         │
         ▼
[SignalR Hub.Clients.Group("{roomCode}").SendAsync("GameState", state)]
         │
         ▼
[Blazor WebAssembly (C1) SignalR client receives state]
         │
         ▼
[Blazor component re-renders game grid]
         │
         ▼
[Browser renders updated frame]
```

### Latency budget

| Step | Target latency |
|------|---------------|
| Tick interval | 150 ms |
| Orleans grain call (local silo) | < 5 ms |
| Redis publish/subscribe | < 5 ms |
| SignalR push to Blazor | < 10 ms |
| Blazor → browser re-render | < 16 ms (1 frame) |
| **Total perceived latency** | **~175 ms per frame** |

This is acceptable for a Snake game and is a great discussion point about **game tick rate vs. responsiveness** in the workshop.

---

## 🔧 Custom KEDA External Scaler

### Why a Custom Scaler?

Orleans is self-healing and distributes grain activations across silos automatically, but it cannot tell the ACA platform how many silos it needs. Standard KEDA scalers measure external pressure (queue depth, HTTP RPS, CPU) — they have no visibility into Orleans internals.

The right scaling signal for the Orleans silo tier is: **how many active game rooms (`GameGrain` activations) exist, and how many silos are needed to host them comfortably?** A custom KEDA External Scaler lets us express exactly that logic in application code, then hand the result to KEDA's standard scaling engine.

**Scaling formula:**

```
desiredSiloReplicas = ceil(activeGameGrainCount / targetGrainsPerReplica)
```

| activeGameGrainCount | targetGrainsPerReplica | desiredReplicas |
|---------------------|----------------------|-----------------|
| 0 | 8 | 0 (clamped to minReplicas = 2) |
| 7 | 8 | 1 (clamped to 2) |
| 9 | 8 | 2 |
| 17 | 8 | 3 |
| 40 | 8 | 5 |

KEDA performs this division automatically — the scaler only needs to report the raw `activeGameGrainCount`. The `targetGrainsPerReplica` value is passed as metadata in the ACA scale rule.

---

### The KEDA External Scaler Protocol

KEDA External Scalers communicate over **gRPC** using a well-defined protobuf contract published by the KEDA project. Add the proto file to the project as a build-time code generator:

**`Protos/externalscaler.proto`**

```protobuf
syntax = "proto3";

option csharp_namespace = "UltimateSnake.KedaScaler";

package externalscaler;

service ExternalScaler {
  rpc IsActive(ScaledObjectRef)       returns (IsActiveResponse)    {}
  rpc StreamIsActive(ScaledObjectRef) returns (stream IsActiveResponse) {}
  rpc GetMetricSpec(ScaledObjectRef)  returns (GetMetricSpecResponse) {}
  rpc GetMetrics(GetMetricsRequest)   returns (GetMetricsResponse)  {}
}

message ScaledObjectRef {
  string name      = 1;
  string namespace = 2;
  map<string, string> scalerMetadata = 3;
}

message IsActiveResponse      { bool result = 1; }

message GetMetricSpecResponse { repeated MetricSpec  metricSpecs  = 1; }
message GetMetricsResponse    { repeated MetricValues metricValues = 1; }

message MetricSpec   { string metricName = 1; int64 targetSize   = 2; }
message MetricValues { string metricName = 1; int64 metricValue  = 2; }

message GetMetricsRequest {
  ScaledObjectRef scaledObjectRef = 1;
  string          metricName      = 2;
}
```

Add the proto reference to the `.csproj` so the gRPC toolchain generates the server-side base classes:

```xml
<ItemGroup>
  <Protobuf Include="Protos\externalscaler.proto" GrpcServices="Server" />
</ItemGroup>
```

KEDA calls the four RPC methods in this order:
1. `IsActive` — should the scaled object be active at all? Return `false` to scale to zero (subject to `minReplicas`).
2. `GetMetricSpec` — what is the metric name and its target value per replica?
3. `GetMetrics` — what is the **current** raw metric value? KEDA computes `ceil(value / target)`.
4. `StreamIsActive` — optional streaming variant of `IsActive` for push-based active/inactive signalling.

---

### Querying Orleans via IManagementGrain

Orleans ships a built-in `IManagementGrain` (always activated with key `0`) that exposes cluster-wide observability without any custom instrumentation.

**Useful methods:**

```csharp
// All silos in the cluster and their runtime stats (CPU, memory, activation count)
SiloRuntimeStatistics[] siloStats = await managementGrain.GetRuntimeStatistics();

// Per-grain-type activation counts, optionally filtered to specific types
DetailedGrainStatistic[] grainStats = await managementGrain.GetDetailedGrainStatistics(
    types: new[] { "UltimateSnake.Orleans.Grains.GameGrain" });
```

The scaler uses **both** queries:

| Query | What it tells us |
|-------|-----------------|
| `GetDetailedGrainStatistics(["GameGrain"])` | Total active game rooms across the entire cluster → **primary scaling metric** |
| `GetRuntimeStatistics()` | Per-silo `ActivationCount` and `IsOverloaded` flag → **secondary overload signal** |

**Caching:** `IManagementGrain` calls involve inter-silo messaging. Cache the result in memory for **10 seconds** — KEDA's polling interval is typically 15–30 seconds, so a short cache avoids redundant cluster gossip without staling the metric:

```csharp
private sealed class GrainStatsCache(IClusterClient clusterClient)
{
    private const string GameGrainTypeName = "UltimateSnake.Orleans.Grains.GameGrain";
    private readonly SemaphoreSlim _lock = new(1, 1);
    private (long ActiveGames, bool AnyOverloaded, DateTime Expiry) _cached;

    public async Task<(long ActiveGames, bool AnyOverloaded)> GetAsync()
    {
        if (DateTime.UtcNow < _cached.Expiry)
            return (_cached.ActiveGames, _cached.AnyOverloaded);

        await _lock.WaitAsync();
        try
        {
            if (DateTime.UtcNow < _cached.Expiry)
                return (_cached.ActiveGames, _cached.AnyOverloaded);

            var mgmt = clusterClient.GetGrain<IManagementGrain>(0);

            var grainStats = await mgmt.GetDetailedGrainStatistics(
                new[] { GameGrainTypeName });
            long activeGames = grainStats.Sum(s => s.ActivationCount);

            var siloStats = await mgmt.GetRuntimeStatistics();
            bool anyOverloaded = siloStats.Any(s => s.IsOverloaded);

            _cached = (activeGames, anyOverloaded, DateTime.UtcNow.AddSeconds(10));
            return (activeGames, anyOverloaded);
        }
        finally { _lock.Release(); }
    }
}
```

---

### gRPC Service Implementation

The gRPC service inherits from `ExternalScaler.ExternalScalerBase` (generated from the proto) and injects the `GrainStatsCache`:

```csharp
public sealed class OrleansGrainScalerService(GrainStatsCache cache)
    : ExternalScaler.ExternalScalerBase
{
    private const string MetricName = "activeGameGrains";

    // KEDA asks: is this scaled object active at all?
    // Return false only when there are zero active games AND no overloaded silos.
    public override async Task<IsActiveResponse> IsActive(
        ScaledObjectRef request, ServerCallContext context)
    {
        var (activeGames, anyOverloaded) = await cache.GetAsync();
        return new IsActiveResponse { Result = activeGames > 0 || anyOverloaded };
    }

    // KEDA asks: what metric do you expose, and what is the per-replica target?
    // The target comes from the scale rule metadata so it is configurable per deployment.
    public override Task<GetMetricSpecResponse> GetMetricSpec(
        ScaledObjectRef request, ServerCallContext context)
    {
        request.ScalerMetadata.TryGetValue("targetGrainsPerReplica", out var raw);
        long target = long.TryParse(raw, out var t) ? t : 8;

        return Task.FromResult(new GetMetricSpecResponse
        {
            MetricSpecs =
            {
                new MetricSpec { MetricName = MetricName, TargetSize = target }
            }
        });
    }

    // KEDA asks: what is the current raw metric value?
    // KEDA computes desiredReplicas = ceil(metricValue / targetSize) internally.
    public override async Task<GetMetricsResponse> GetMetrics(
        GetMetricsRequest request, ServerCallContext context)
    {
        var (activeGames, anyOverloaded) = await cache.GetAsync();

        // If any silo is reporting overload, bump the count by one full target
        // worth of grains to force at least one extra replica.
        var adjustedCount = anyOverloaded
            ? activeGames + (long.TryParse(
                request.ScaledObjectRef.ScalerMetadata.GetValueOrDefault("targetGrainsPerReplica"),
                out var t) ? t : 8)
            : activeGames;

        return new GetMetricsResponse
        {
            MetricValues =
            {
                new MetricValues { MetricName = MetricName, MetricValue = adjustedCount }
            }
        };
    }

    // Streaming variant — keeps the connection open and pushes IsActive updates.
    // Useful for near-instant scale-to-zero when the last game ends.
    public override async Task StreamIsActive(
        ScaledObjectRef request,
        IServerStreamWriter<IsActiveResponse> responseStream,
        ServerCallContext context)
    {
        while (!context.CancellationToken.IsCancellationRequested)
        {
            var (activeGames, anyOverloaded) = await cache.GetAsync();
            await responseStream.WriteAsync(
                new IsActiveResponse { Result = activeGames > 0 || anyOverloaded });
            await Task.Delay(TimeSpan.FromSeconds(15), context.CancellationToken);
        }
    }
}
```

**`Program.cs`** — wire up Orleans client + gRPC:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddSingleton<GrainStatsCache>();

// Orleans client connects to the silo cluster (Container 3)
builder.UseOrleansClient(client =>
{
    client.UseAzureStorageClustering(options =>
        options.ConfigureTableServiceClient(
            builder.Configuration["Orleans:ClusteringConnectionString"]));
    client.Configure<ClusterOptions>(options =>
    {
        options.ClusterId = "ultimate-snake";
        options.ServiceId = "ultimate-snake";
    });
});

var app = builder.Build();
app.MapGrpcService<OrleansGrainScalerService>();
app.Run();
```

---

### Deployment and ACA Scale Rule

#### Container App — the scaler service

The KEDA scaler runs as a Container App with **internal-only ingress on port 9090** (gRPC transport). It must be in the same ACA Environment as the Orleans silo so it can reach the clustering Table Storage.

```bicep
resource kedaScaler 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'orleans-keda-scaler'
  location: location
  properties: {
    environmentId: acaEnvironment.id
    configuration: {
      ingress: {
        external: false          // internal only — never exposed to the internet
        targetPort: 9090
        transport: 'http2'       // required for gRPC
      }
      secrets: [
        { name: 'orleans-clustering-cs', value: clusteringConnectionString }
      ]
    }
    template: {
      containers: [
        {
          name: 'keda-scaler'
          image: '${acr.properties.loginServer}/ultimate-snake-keda-scaler:latest'
          env: [
            { name: 'Orleans__ClusteringConnectionString', secretRef: 'orleans-clustering-cs' }
          ]
          resources: { cpu: '0.25', memory: '0.5Gi' }
        }
      ]
      scale: {
        minReplicas: 1    // the scaler itself must always be running
        maxReplicas: 1    // only one replica needed — KEDA talks to one endpoint
      }
    }
  }
}
```

#### Scale rule on Container 3 (Orleans silo)

Add a custom scale rule to the Orleans silo Container App that points KEDA at the scaler's internal FQDN:

```bicep
resource orleansSilo 'Microsoft.App/containerApps@2024-03-01' = {
  // ... other properties ...
  properties: {
    template: {
      scale: {
        minReplicas: 2      // always keep at least 2 silos for resilience
        maxReplicas: 10
        rules: [
          {
            name: 'orleans-grain-count'
            custom: {
              type: 'external'
              metadata: {
                // Internal FQDN of Container 5 — ACA resolves this within the environment
                scalerAddress: 'orleans-keda-scaler:9090'
                targetGrainsPerReplica: '8'
              }
            }
          }
        ]
      }
    }
  }
}
```

> **Note on `scalerAddress`:** Within an ACA Environment, containers reach each other by app name. No port suffix is needed in the DNS name; KEDA resolves the gRPC port via the ingress configuration.

#### Verifying the scaler

Participants can test the scaler in isolation using [grpcurl](https://github.com/fullstorydev/grpcurl) before wiring it to KEDA:

```bash
# Check IsActive
grpcurl -plaintext -d '{"name":"orleans-silo","namespace":"default"}' \
  orleans-keda-scaler:9090 externalscaler.ExternalScaler/IsActive

# Check current metric value
grpcurl -plaintext -d '{
  "scaledObjectRef": {"name":"orleans-silo","namespace":"default","scalerMetadata":{"targetGrainsPerReplica":"8"}},
  "metricName":"activeGameGrains"
}' \
  orleans-keda-scaler:9090 externalscaler.ExternalScaler/GetMetrics
```

---

### Advanced: Proactive Scaling with StreamIsActive

The standard `IsActive` + `GetMetrics` polling pattern has an inherent lag: KEDA only checks the metric on its polling interval (15–30 s by default). For a Snake game, a burst of players creating rooms simultaneously could overload silos before KEDA reacts.

`StreamIsActive` solves this: the scaler keeps a long-lived gRPC server-streaming response open and **pushes** active/inactive transitions as they happen. KEDA re-polls `GetMetrics` immediately upon receiving a push, cutting the scale-out latency to near-zero.

In the implementation above, `StreamIsActive` re-evaluates grain stats every 15 seconds. For a tighter response, subscribe to Orleans Streams (or a Redis Pub/Sub channel that the API writes to when rooms are created) and push an active signal the moment a new game starts:

```csharp
public override async Task StreamIsActive(
    ScaledObjectRef request,
    IServerStreamWriter<IsActiveResponse> responseStream,
    ServerCallContext context)
{
    // Subscribe to Redis channel that the API publishes to on room creation/end
    var sub = _redis.GetSubscriber();
    await sub.SubscribeAsync("room:lifecycle", async (_, _) =>
    {
        // A room was created or ended — push an immediate signal to KEDA
        var (activeGames, _) = await cache.GetAsync();
        await responseStream.WriteAsync(
            new IsActiveResponse { Result = activeGames > 0 });
    });

    // Keep the stream open until KEDA disconnects
    await Task.Delay(Timeout.Infinite, context.CancellationToken);
    await sub.UnsubscribeAsync("room:lifecycle");
}
```

This is an excellent advanced exercise that combines **gRPC server streaming**, **Redis Pub/Sub**, and **KEDA proactive scaling** in one cohesive flow.

---

## 🚀 Extension Ideas

---

### 💾 Redis Cache

Redis is introduced in the core architecture as the **SignalR backplane** (enabling multiple API replicas) and as the **tick pub/sub channel**. The following exercises extend this further:

#### 1. Active rooms registry

The Game Tick Service needs to know which rooms are currently active without querying Orleans on every poll cycle. Store the active room codes in a **Redis SET**:

```
SADD active-rooms "TIGER-4821"
SREM active-rooms "TIGER-4821"   ← on game end
SMEMBERS active-rooms            ← tick service reads this
```

The API updates this set when a game starts or ends. The tick service reads it every few seconds (or on Redis Pub/Sub notification) to refresh its list.

#### 2. Leaderboard with Redis Sorted Sets

Store the all-time leaderboard as a **Redis Sorted Set** — the most natural structure for ranked data:

```
ZADD leaderboard:alltime <score> "<playerId>:<playerName>"
ZREVRANGE leaderboard:alltime 0 9 WITHSCORES   ← top 10
ZRANK leaderboard:alltime "<playerId>:..."     ← player's rank
```

This gives **O(log N) inserts and rank lookups** — far faster than scanning a database table. Scores can be updated atomically with `ZADD NX GT` to only update if the new score is higher.

#### 3. Session leaderboard (per room, short TTL)

Each game room has a transient leaderboard stored in Redis with a **TTL** matching the expected game duration plus a grace period:

```
ZADD leaderboard:room:TIGER-4821 <score> "<playerId>"
EXPIRE leaderboard:room:TIGER-4821 3600
```

This avoids polluting the durable leaderboard store with in-progress game data.

#### 4. Rate limiting room joins

Use Redis `INCR` + `EXPIRE` to rate-limit how many join attempts a given IP can make per minute — a simple but effective demonstration of Redis as a rate-limiting primitive.

#### 5. Distributed lock for the tick service

If participants scale the tick service to multiple replicas, introduce a **Redis distributed lock** (using `SET NX PX`) per room code. Only the replica holding the lock ticks that room, preventing duplicate ticks.

---

### ⚙️ Azure Container Jobs

Azure Container Jobs run containerised workloads on a schedule or in response to an event, without a long-lived container. They are perfect for batch operations that do not need to run continuously.

#### Option A — Cron Job: Weekly Leaderboard Snapshot

A **schedule-triggered Container Job** runs every Monday at midnight UTC. It:

1. Reads the top 100 scores from the Redis Sorted Set leaderboard.
2. Writes a JSON snapshot to an Azure Blob Storage container (`leaderboard-archive/YYYY-WW.json`).
3. Resets the `leaderboard:weekly` sorted set in Redis for the new week.
4. (Optional) Sends a "Top 10 of the week" notification — e.g., writes a record to a `notifications` table in Azure Table Storage that the frontend polls.

```yaml
# Azure Container Apps Job (cron)
schedule: "0 0 * * MON"
triggerType: Schedule
replicaCompletionCount: 1
```

This is an excellent demonstration of the **separation between always-on services and batch workloads** — the job container image is identical in structure to a regular Worker Service, but it exits with code 0 when done rather than looping forever.

#### Option B — Queue-Triggered Job: Game Result Processor

When a game ends, the API publishes a `GameEndedEvent` message to an **Azure Service Bus Queue**:

```json
{
  "roomCode": "TIGER-4821",
  "mode": "Versus",
  "endedAt": "2026-04-10T22:00:00Z",
  "players": [
    { "playerId": "abc123", "name": "Alice", "score": 47, "rank": 1 },
    { "playerId": "def456", "name": "Bob",   "score": 31, "rank": 2 }
  ]
}
```

A **Service Bus queue-triggered Container Job** starts a replica for each message:

1. Reads the `GameEndedEvent` from the queue.
2. Updates the all-time leaderboard in Redis Sorted Set.
3. Writes the full game result to Azure Table Storage for historical querying.
4. Checks for achievements (first win, longest snake ever, etc.) and writes them to a player achievements table.
5. Exits. The job replica disappears — no idle compute cost.

```yaml
triggerType: Event
eventTriggerConfig:
  scale:
    minExecutions: 0
    maxExecutions: 10
    rules:
      - name: azure-servicebus-queue-rule
        type: azure-servicebus
        metadata:
          queueName: game-ended
          messageCount: "1"
```

This demonstrates the **KEDA-based event-driven scaling** that Azure Container Apps Jobs support natively.

#### Option C — Cron Job: Stale Room Cleanup

Games can be abandoned (browser closed, network drop). A **daily cleanup job** scans the Orleans grain state in Azure Blob Storage for rooms that have been inactive for more than 24 hours and deletes their grain state blobs — keeping storage costs low and demonstrating blob lifecycle management from a Container Job.

---

### 🎭 Dapr Integration

[Dapr](https://dapr.io/) (Distributed Application Runtime) is a sidecar-based runtime that Azure Container Apps supports natively — simply enable the Dapr sidecar on a container app and it appears as a local HTTP/gRPC endpoint. No SDK changes are required to enable the sidecar, but using the Dapr SDK makes the integration much cleaner.

#### 1. Dapr Pub/Sub: Replace Redis Pub/Sub for tick events

The current architecture uses Redis Pub/Sub directly (via `StackExchange.Redis`). Replace this with **Dapr Pub/Sub**, backed by the same Redis instance (or Azure Service Bus for durability):

**Before (direct Redis):**
```csharp
await _redisSubscriber.PublishAsync($"game:tick:{roomCode}", serialisedState);
```

**After (Dapr):**
```csharp
await _daprClient.PublishEventAsync("pubsub", $"game-tick-{roomCode}", gameState);
```

The Container 2 subscriber becomes a Dapr subscription endpoint:
```csharp
app.MapPost("/game-tick/{roomCode}", [Topic("pubsub", "game-tick-{roomCode}")] async (GameState state, IHubContext<GameHub> hub) =>
{
    await hub.Clients.Group(state.RoomCode).SendAsync("GameState", state);
});
```

**Why this is a great workshop exercise:**
- Shows how Dapr **decouples the messaging infrastructure** from the application code.
- Swapping the pubsub component from Redis to Azure Service Bus requires only a YAML component file change — zero code changes.
- Introduces the concept of **Dapr components** and how they are configured in Azure Container Apps.

#### 2. Dapr Service Invocation: Frontend → API calls

Replace direct HTTP calls from the Blazor frontend (Container 1) to the API (Container 2) with **Dapr service invocation**:

```csharp
// Before: direct HTTP
var response = await _httpClient.PostAsJsonAsync("https://game-api/rooms", request);

// After: Dapr service invocation (with retries, tracing, mTLS built-in)
var response = await _daprClient.InvokeMethodAsync<CreateRoomRequest, RoomInfo>(
    HttpMethod.Post, "game-api", "rooms", request);
```

Benefits demonstrated:
- **Automatic retries** on transient failures.
- **Built-in distributed tracing** (Zipkin-compatible, visible in Azure Monitor).
- **mTLS** between sidecars (secure service-to-service without manual certificate management).

#### 3. Dapr State Store: Supplement or replace Orleans persistence

For players who are not yet in an active game, their profile data (name, all-time stats) can be stored using the **Dapr state store** component (backed by Azure Blob Storage or Redis):

```csharp
await _daprClient.SaveStateAsync("statestore", $"player-{playerId}", playerProfile);
var profile = await _daprClient.GetStateAsync<PlayerProfile>("statestore", $"player-{playerId}");
```

This cleanly separates **ephemeral game state** (Orleans grains, short-lived, in-memory-first) from **durable player profile state** (Dapr state store, always persisted).

#### 4. Dapr Bindings: Trigger the Container Job

Use a **Dapr output binding** for Azure Service Bus to publish the `GameEndedEvent` from the API container. The Dapr sidecar handles serialisation, retry, and connection management:

```csharp
await _daprClient.InvokeBindingAsync("game-ended-queue", "create", gameEndedEvent);
```

This is the binding that triggers the **Option B Container Job** above — a clean end-to-end demonstration of event-driven architecture entirely through Dapr.

#### 5. Dapr Actors: Orleans-lite mode (advanced)

For participants who want to explore an alternative to Orleans, the grain pattern can be replicated using **Dapr Actors**:

- `PlayerActor` (actor type: `Player`, actor ID: `playerId`)
- `GameActor` (actor type: `Game`, actor ID: `roomCode`)
- `SnakeActor` (actor type: `Snake`, actor ID: `{roomCode}:{playerId}`)

Dapr Actors use Redis (or another state store) for activation and state persistence. This is a fascinating architectural comparison exercise: Orleans gives you more power and a richer ecosystem; Dapr Actors give you portability across any language.

---

### 🏆 Leaderboards

Leaderboards are an excellent feature to make the workshop feel alive — especially when multiple workshop groups are competing in the same Azure environment. Here are layered ideas from simple to advanced:

#### Tier 1 — In-game session leaderboard (baseline)

The simplest leaderboard is displayed at the end of a game, showing all players in that room ranked by score. This is just the `Scores` dictionary from `GameState`, sorted. No extra infrastructure needed — implement this first.

#### Tier 2 — All-time leaderboard with Redis Sorted Set

As described in the Redis section, store the top scores in a Redis Sorted Set. Display a `/leaderboard` page in the Blazor frontend showing:

| Rank | Player | Score | Date |
|------|--------|-------|------|
| 🥇 1 | Alice  | 247   | Apr 10 |
| 🥈 2 | Bob    | 198   | Apr 10 |

Update the leaderboard atomically when a game ends (via the Container Job for reliability, or directly from the API for simplicity).

#### Tier 3 — Real-time leaderboard with live rank updates

Make the leaderboard page **live** using SignalR: when a score changes during an active game, push a leaderboard update event to all clients subscribed to `/hubs/game` in the `leaderboard` group. Watching your rank change in real time during a game is highly engaging.

Participants learn about **SignalR groups** and fan-out messaging patterns.

#### Tier 4 — Category leaderboards

Expand to multiple leaderboard dimensions, each backed by its own Redis Sorted Set:

| Leaderboard | Redis key | Sort metric |
|-------------|-----------|-------------|
| All-time high score | `lb:alltime:score` | Final score |
| Longest snake ever | `lb:alltime:length` | Max snake length reached |
| Most kills (Versus) | `lb:alltime:kills` | Total opponents killed |
| Fastest game win | `lb:alltime:speed` | Ticks to beat opponent (lower = better, use negative score) |
| Weekly score | `lb:weekly:{YYYY-WW}` | Score in current week |

Each column on the leaderboard page filters to a different sorted set. This is a great Redis data modelling exercise.

#### Tier 5 — Workshop group competition

When running the workshop with multiple groups, each group builds their own deployment. A **shared leaderboard** aggregates scores across all groups:

1. Each group's Container Job posts their top scores to a shared **Azure Table Storage** table in a common storage account.
2. A shared leaderboard frontend (a separate static Blazor WASM page) queries this table and shows cross-group rankings.
3. The Container Job is the same cron job from [Option A](#option-a--cron-job-weekly-leaderboard-snapshot) — each group schedules it independently, all writing to the same shared partition.

This introduces **multi-tenant data isolation** (partition key = group name) and demonstrates how Container Apps in different environments can share Azure resources.

#### Tier 6 — Achievements system (advanced)

Introduce an achievements system to reward specific accomplishments:

| Achievement | Trigger |
|-------------|---------|
| 🐍 First Blood | First food eaten in a game |
| 📏 Going Long | Snake reaches 20 tiles |
| 💀 Assassin | Kill 3 snakes in one Versus session |
| 🌯 The Wrap | Wrap around the edge 10 times in one game |
| 👑 Untouchable | Win a Versus game without dying |
| ⚡ Speed Demon | Complete a game in under 60 seconds |

Achievements are evaluated in the **Queue-Triggered Container Job** (Option B) after each game. They are stored in Azure Table Storage (partitioned by `playerId`) and displayed on the leaderboard page as badges. This gives participants experience with **event-driven post-processing** and Table Storage entity design.

---

## 📁 Solution Structure

```
lab-project/
│
├── README.md                          ← This file
│
├── src/
│   ├── UltimateSnake.sln
│   │
│   ├── UltimateSnake.Frontend/        ← Container 1 (Blazor WebAssembly hosted)
│   │   ├── Components/
│   │   │   ├── Pages/
│   │   │   │   ├── Home.razor
│   │   │   │   ├── Room.razor
│   │   │   │   ├── Game.razor
│   │   │   │   └── Leaderboard.razor
│   │   │   └── Shared/
│   │   │       ├── SnakeCanvas.razor
│   │   │       └── PlayerList.razor
│   │   ├── Services/
│   │   │   ├── GameApiClient.cs       ← Typed HTTP client to API
│   │   │   └── GameHubClient.cs       ← SignalR client wrapper
│   │   ├── Program.cs
│   │   └── Dockerfile
│   │
│   ├── UltimateSnake.Api/             ← Container 2 (Minimal API)
│   │   ├── Endpoints/
│   │   │   ├── RoomEndpoints.cs
│   │   │   └── LeaderboardEndpoints.cs
│   │   ├── Hubs/
│   │   │   └── GameHub.cs             ← SignalR Hub
│   │   ├── Services/
│   │   │   └── RedisGameStateRelay.cs ← Subscribes Redis, pushes to Hub
│   │   ├── Program.cs
│   │   └── Dockerfile
│   │
│   ├── UltimateSnake.Orleans/         ← Container 3 (Orleans Silo)
│   │   ├── Grains/
│   │   │   ├── PlayerGrain.cs
│   │   │   ├── GameGrain.cs
│   │   │   └── SnakeGrain.cs
│   │   ├── State/
│   │   │   ├── PlayerState.cs
│   │   │   ├── GameRoomState.cs
│   │   │   └── SnakeGrainState.cs
│   │   ├── Program.cs
│   │   └── Dockerfile
│   │
│   ├── UltimateSnake.TickService/     ← Container 4 (Worker Service)
│   │   ├── Workers/
│   │   │   └── GameTickWorker.cs
│   │   ├── Program.cs
│   │   └── Dockerfile
│   │
│   ├── UltimateSnake.KedaScaler/      ← Container 5 (gRPC External Scaler)
│   │   ├── Protos/
│   │   │   └── externalscaler.proto   ← KEDA external scaler contract
│   │   ├── Services/
│   │   │   ├── OrleansGrainScalerService.cs  ← ExternalScaler gRPC impl
│   │   │   └── GrainStatsCache.cs     ← IManagementGrain result cache
│   │   ├── Program.cs
│   │   └── Dockerfile
│   │
│   ├── UltimateSnake.LeaderboardJob/  ← Container Job
│   │   ├── Program.cs                 ← Runs, does work, exits
│   │   └── Dockerfile
│   │
│   └── UltimateSnake.Contracts/       ← Shared types (no Dockerfile)
│       ├── Grains/
│       │   ├── IPlayerGrain.cs
│       │   ├── IGameGrain.cs
│       │   └── ISnakeGrain.cs
│       └── Models/
│           ├── GameState.cs
│           ├── SnakeState.cs
│           ├── Position.cs
│           ├── Direction.cs
│           └── GameMode.cs
│
└── infra/
    ├── main.bicep                     ← ACA environment, all apps, jobs
    ├── storage.bicep                  ← Blob + Table Storage accounts
    ├── redis.bicep                    ← Azure Cache for Redis
    ├── servicebus.bicep               ← Service Bus namespace + queue
    └── roles.bicep                    ← Managed Identity role assignments
```

---

## 🗺️ Workshop Journey

The lab project is built progressively across the workshop chapters. Each chapter adds a new Azure Container Apps concept by extending the game.

| Chapter | What participants build | ACA concept introduced |
|---------|------------------------|------------------------|
| **Ch. 1** | Dockerise the Blazor frontend | Container images, Dockerfiles, ACR |
| **Ch. 2** | Deploy the frontend to ACA | Ingress, environment variables, scaling to zero |
| **Ch. 3** | Add the Minimal API container | Internal ingress, service discovery, DAPR sidecar basics |
| **Ch. 4** | Add Orleans (single silo) | Multi-container apps, health probes, startup ordering |
| **Ch. 5** | Scale Orleans to multiple silos | Scaling rules, Azure Table Storage clustering, replica state |
| **Ch. 6** | Add the Game Tick Service + Redis | Worker containers, Redis Cache, pub/sub patterns |
| **Ch. 7** | Managed Identity & Key Vault | Secrets management, Managed Identity, secret references |
| **Ch. 8** | Build the KEDA External Scaler | Custom KEDA scalers, gRPC, IManagementGrain, external scale rules |
| **Ch. 9** | Add the Leaderboard Container Job | ACA Jobs, cron triggers, Service Bus queue triggers |
| **Ch. 10** | Introduce Dapr | Dapr pub/sub, service invocation, state store, components |
| **Ch. 11** | Observability & production readiness | Log Analytics, metrics, revision management, traffic splitting |

> **Tip for facilitators:** The `lab-project/src/` directory contains a completed reference solution. Participants work from stubs in the `exercises/` directory. At any point, they can compare their work against the reference solution.

---

*Happy slithering! 🐍*
