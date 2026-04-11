namespace UltimateSnake.Frontend.Client.Models;

public enum Direction { Up, Down, Left, Right }

public enum GamePhase { NameEntry, RoomLobby, WaitingLobby, Countdown, Playing, Spectating, Leaderboard }

public enum GameStatus { Waiting, Playing, GameOver }

public readonly record struct Position(int X, int Y);

// API request/response models
public record CreatePlayerRequest(string Name);
public record CreatePlayerResponse(Guid PlayerId, string Name);

public record CreateRoomRequest(Guid PlayerId, string PlayerName);
public record CreateRoomResponse(
    string RoomCode, int TickIntervalMs, Guid SnakeId,
    Position Food, List<Position> InitialPositions, string InitialDirection);

public record JoinRoomRequest(Guid PlayerId, string PlayerName);
public record JoinRoomResponse(
    string RoomCode, int TickIntervalMs, Guid SnakeId,
    Position Food, List<Position> InitialPositions, string InitialDirection);

public record RoomStateResponse(
    string Code, int TickIntervalMs, bool IsActive,
    List<SnakeStateDto> Snakes, GameStatus GameStatus, Guid HostSnakeId);

public record SnakeStateDto(
    Guid Id, Guid PlayerId, string PlayerName,
    List<Position> Positions, string Direction, int Score, bool IsAlive,
    bool IsReady, Position Food);

public record UpdateSnakeRequest(List<Position> Positions, string Direction, int Score, bool IsAlive);
public record SetReadyRequest(bool IsReady);
public record StartGameRequest(Guid HostSnakeId);
