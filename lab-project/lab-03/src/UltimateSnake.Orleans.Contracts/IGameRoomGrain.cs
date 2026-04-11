namespace UltimateSnake.Orleans.Contracts;

public interface IGameRoomGrain : IGrainWithStringKey
{
    Task<CreateRoomResult> CreateAsync(Guid hostPlayerId, string hostPlayerName);
    Task<JoinRoomResult> JoinAsync(Guid playerId, string playerName);
    Task<RoomStateResult> GetStateAsync();
    Task<RoomStateResult> SetReadyAsync(Guid snakeId, bool isReady);
    Task<RoomStateResult> StartGameAsync(Guid hostSnakeId);
    Task<RoomStateResult> UpdateSnakeAsync(Guid snakeId, UpdateSnakeData update);
    Task LeaveAsync(Guid snakeId);
}
