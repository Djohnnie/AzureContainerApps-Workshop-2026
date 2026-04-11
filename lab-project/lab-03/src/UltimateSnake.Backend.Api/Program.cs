using Azure.Data.Tables;
using Orleans.Configuration;
using UltimateSnake.Orleans.Contracts;

var builder = WebApplication.CreateBuilder(args);

#if ASPIRE
builder.AddServiceDefaults();
#endif

builder.UseOrleansClient(client =>
{
#if ASPIRE
    client.UseLocalhostClustering();
#else
    var connectionString = builder.Configuration["AZURE_STORAGE_CONNECTION_STRING"]
        ?? throw new InvalidOperationException("AZURE_STORAGE_CONNECTION_STRING is not set.");
    client.UseAzureStorageClustering(options =>
        options.TableServiceClient = new TableServiceClient(connectionString));
#endif

    client.Configure<ClusterOptions>(options =>
    {
        options.ClusterId = "lab-03";
        options.ServiceId = "lab-03";
    });
});

var app = builder.Build();

#if ASPIRE
app.MapDefaultEndpoints();
#endif

app.MapGet("/server-name", () => new { ServerName = Environment.MachineName });

// Players
app.MapPost("/api/players", async (CreatePlayerRequest req, IGrainFactory grains) =>
{
    var playerId = Guid.NewGuid();
    var grain = grains.GetGrain<IPlayerGrain>(playerId);
    await grain.InitAsync(req.Name);
    return Results.Ok(new { PlayerId = playerId, Name = req.Name });
});

// Rooms
app.MapPost("/api/rooms", async (CreateRoomRequest req, IGrainFactory grains) =>
{
    var code = GenerateRoomCode();
    var grain = grains.GetGrain<IGameRoomGrain>(code);
    var result = await grain.CreateAsync(req.PlayerId, req.PlayerName);
    return Results.Ok(result);
});

app.MapPost("/api/rooms/{roomCode}/join", async (string roomCode, JoinRoomRequest req, IGrainFactory grains) =>
{
    var grain = grains.GetGrain<IGameRoomGrain>(roomCode.ToUpperInvariant());
    var result = await grain.JoinAsync(req.PlayerId, req.PlayerName);
    return Results.Ok(result);
});

app.MapGet("/api/rooms/{roomCode}", async (string roomCode, IGrainFactory grains) =>
{
    var grain = grains.GetGrain<IGameRoomGrain>(roomCode.ToUpperInvariant());
    var result = await grain.GetStateAsync();
    return Results.Ok(result);
});

app.MapPost("/api/rooms/{roomCode}/snakes/{snakeId}/ready", async (
    string roomCode, Guid snakeId, SetReadyRequest req, IGrainFactory grains) =>
{
    var grain = grains.GetGrain<IGameRoomGrain>(roomCode.ToUpperInvariant());
    var result = await grain.SetReadyAsync(snakeId, req.IsReady);
    return Results.Ok(result);
});

app.MapPost("/api/rooms/{roomCode}/start", async (
    string roomCode, StartGameRequest req, IGrainFactory grains) =>
{
    var grain = grains.GetGrain<IGameRoomGrain>(roomCode.ToUpperInvariant());
    var result = await grain.StartGameAsync(req.HostSnakeId);
    return Results.Ok(result);
});

app.MapPost("/api/rooms/{roomCode}/snakes/{snakeId}", async (
    string roomCode, Guid snakeId, UpdateSnakeData update, IGrainFactory grains) =>
{
    var grain = grains.GetGrain<IGameRoomGrain>(roomCode.ToUpperInvariant());
    var result = await grain.UpdateSnakeAsync(snakeId, update);
    return Results.Ok(result);
});

app.MapDelete("/api/rooms/{roomCode}/snakes/{snakeId}", async (
    string roomCode, Guid snakeId, IGrainFactory grains) =>
{
    var grain = grains.GetGrain<IGameRoomGrain>(roomCode.ToUpperInvariant());
    await grain.LeaveAsync(snakeId);
    return Results.NoContent();
});

app.Run();

string GenerateRoomCode()
{
    const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    var rng = Random.Shared;
    return new string(Enumerable.Range(0, 6).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
}

record CreatePlayerRequest(string Name);
record CreateRoomRequest(Guid PlayerId, string PlayerName);
record JoinRoomRequest(Guid PlayerId, string PlayerName);
record SetReadyRequest(bool IsReady);
record StartGameRequest(Guid HostSnakeId);
