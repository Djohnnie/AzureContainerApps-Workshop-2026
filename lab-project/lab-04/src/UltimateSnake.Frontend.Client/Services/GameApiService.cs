using System.Net.Http.Json;
using UltimateSnake.Frontend.Client.Models;

namespace UltimateSnake.Frontend.Client.Services;

public class GameApiService(HttpClient http)
{
    public async Task<CreatePlayerResponse?> CreatePlayerAsync(string name)
    {
        var response = await http.PostAsJsonAsync("/api/players", new CreatePlayerRequest(name));
        return await response.Content.ReadFromJsonAsync<CreatePlayerResponse>();
    }

    public async Task<CreateRoomResponse?> CreateRoomAsync(Guid playerId, string playerName)
    {
        var response = await http.PostAsJsonAsync("/api/rooms", new CreateRoomRequest(playerId, playerName));
        return await response.Content.ReadFromJsonAsync<CreateRoomResponse>();
    }

    public async Task<JoinRoomResponse?> JoinRoomAsync(string roomCode, Guid playerId, string playerName)
    {
        var response = await http.PostAsJsonAsync($"/api/rooms/{roomCode}/join", new JoinRoomRequest(playerId, playerName));
        return await response.Content.ReadFromJsonAsync<JoinRoomResponse>();
    }

    public async Task<RoomStateResponse?> GetRoomStateAsync(string roomCode)
        => await http.GetFromJsonAsync<RoomStateResponse>($"/api/rooms/{roomCode}");

    public async Task<RoomStateResponse?> SetReadyAsync(string roomCode, Guid snakeId, bool isReady)
    {
        var response = await http.PostAsJsonAsync($"/api/rooms/{roomCode}/snakes/{snakeId}/ready", new SetReadyRequest(isReady));
        return await response.Content.ReadFromJsonAsync<RoomStateResponse>();
    }

    public async Task<RoomStateResponse?> StartGameAsync(string roomCode, Guid hostSnakeId)
    {
        var response = await http.PostAsJsonAsync($"/api/rooms/{roomCode}/start", new StartGameRequest(hostSnakeId));
        return await response.Content.ReadFromJsonAsync<RoomStateResponse>();
    }

    public async Task<RoomStateResponse?> UpdateSnakeAsync(string roomCode, Guid snakeId, UpdateSnakeRequest update)
    {
        var response = await http.PostAsJsonAsync($"/api/rooms/{roomCode}/snakes/{snakeId}", update);
        return await response.Content.ReadFromJsonAsync<RoomStateResponse>();
    }

    public async Task LeaveRoomAsync(string roomCode, Guid snakeId)
        => await http.DeleteAsync($"/api/rooms/{roomCode}/snakes/{snakeId}");
}
