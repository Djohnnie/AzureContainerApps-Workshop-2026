namespace UltimateSnake.Orleans.Contracts;

[GenerateSerializer]
public enum GameStatus { Waiting, Playing, GameOver }

[GenerateSerializer]
public record GrainPosition([property: Id(0)] int X, [property: Id(1)] int Y);

[GenerateSerializer]
public record SnakeStateResult(
    [property: Id(0)] Guid Id,
    [property: Id(1)] Guid PlayerId,
    [property: Id(2)] string PlayerName,
    [property: Id(3)] List<GrainPosition> Positions,
    [property: Id(4)] string Direction,
    [property: Id(5)] int Score,
    [property: Id(6)] bool IsAlive,
    [property: Id(7)] bool IsReady,
    [property: Id(8)] GrainPosition Food
);

[GenerateSerializer]
public record RoomStateResult(
    [property: Id(0)] string Code,
    [property: Id(1)] int TickIntervalMs,
    [property: Id(2)] bool IsActive,
    [property: Id(3)] List<SnakeStateResult> Snakes,
    [property: Id(4)] GameStatus GameStatus,
    [property: Id(5)] Guid HostSnakeId
);

[GenerateSerializer]
public record CreateRoomResult(
    [property: Id(0)] string RoomCode,
    [property: Id(1)] int TickIntervalMs,
    [property: Id(2)] Guid SnakeId,
    [property: Id(3)] GrainPosition Food,
    [property: Id(4)] List<GrainPosition> InitialPositions,
    [property: Id(5)] string InitialDirection
);

[GenerateSerializer]
public record JoinRoomResult(
    [property: Id(0)] string RoomCode,
    [property: Id(1)] int TickIntervalMs,
    [property: Id(2)] Guid SnakeId,
    [property: Id(3)] GrainPosition Food,
    [property: Id(4)] List<GrainPosition> InitialPositions,
    [property: Id(5)] string InitialDirection
);

[GenerateSerializer]
public record UpdateSnakeData(
    [property: Id(0)] List<GrainPosition> Positions,
    [property: Id(1)] string Direction,
    [property: Id(2)] int Score,
    [property: Id(3)] bool IsAlive
);

[GenerateSerializer]
public record PlayerInfo(
    [property: Id(0)] Guid Id,
    [property: Id(1)] string Name
);
