using UltimateSnake.Orleans.Contracts;

namespace UltimateSnake.Orleans.Host.Grains;

public class GameRoomGrain : Grain, IGameRoomGrain
{
    private bool _isActive;
    private GameStatus _gameStatus = GameStatus.Waiting;
    private int _tickIntervalMs = 200;
    private readonly List<SnakeInfo> _snakes = new();
    private Guid _hostSnakeId;
    private readonly Random _rng = new();

    private const int GridSize = 30;

    private static readonly (int X, int Y, string Direction)[] StartPositions =
    [
        (5,  5,  "Right"),
        (24, 24, "Left"),
        (5,  24, "Up"),
        (24, 5,  "Down"),
        (12, 5,  "Right"),
        (12, 24, "Right"),
        (5,  12, "Down"),
        (24, 12, "Up"),
    ];

    public Task<CreateRoomResult> CreateAsync(Guid hostPlayerId, string hostPlayerName)
    {
        if (_isActive)
        {
            var existing = _snakes.FirstOrDefault(s => s.PlayerId == hostPlayerId);
            if (existing != null)
                return Task.FromResult(new CreateRoomResult(
                    this.GetPrimaryKeyString(), _tickIntervalMs, existing.SnakeId,
                    existing.Food, existing.Positions.ToList(), existing.Direction));
        }

        _isActive = true;
        _gameStatus = GameStatus.Waiting;

        var snakeId = Guid.NewGuid();
        _hostSnakeId = snakeId;
        var start = StartPositions[0];
        var positions = MakeInitialSnake(start.X, start.Y, start.Direction);
        var food = GenerateFood(positions.ToHashSet());
        _snakes.Add(new SnakeInfo
        {
            SnakeId = snakeId,
            PlayerId = hostPlayerId,
            PlayerName = hostPlayerName,
            Positions = positions,
            Direction = start.Direction,
            Food = food,
            Score = 0,
            IsAlive = true,
            IsReady = false
        });

        return Task.FromResult(new CreateRoomResult(
            this.GetPrimaryKeyString(), _tickIntervalMs, snakeId,
            food, positions, start.Direction));
    }

    public Task<JoinRoomResult> JoinAsync(Guid playerId, string playerName)
    {
        // Reset room for a fresh game when the previous one ended
        if (_gameStatus == GameStatus.GameOver)
        {
            _snakes.Clear();
            _gameStatus = GameStatus.Waiting;
            _isActive = false;
            _hostSnakeId = Guid.Empty;
        }

        var existing = _snakes.FirstOrDefault(s => s.PlayerId == playerId);
        if (existing != null)
            return Task.FromResult(new JoinRoomResult(
                this.GetPrimaryKeyString(), _tickIntervalMs, existing.SnakeId,
                existing.Food, existing.Positions.ToList(), existing.Direction));

        _isActive = true;
        var startIdx = _snakes.Count % StartPositions.Length;
        var start = StartPositions[startIdx];
        var positions = MakeInitialSnake(start.X, start.Y, start.Direction);
        var food = GenerateFood(positions.ToHashSet());
        var snakeId = Guid.NewGuid();

        if (_hostSnakeId == Guid.Empty)   // first joiner after reset becomes host
            _hostSnakeId = snakeId;

        _snakes.Add(new SnakeInfo
        {
            SnakeId = snakeId,
            PlayerId = playerId,
            PlayerName = playerName,
            Positions = positions,
            Direction = start.Direction,
            Food = food,
            Score = 0,
            IsAlive = true,
            IsReady = false
        });

        return Task.FromResult(new JoinRoomResult(
            this.GetPrimaryKeyString(), _tickIntervalMs, snakeId,
            food, positions, start.Direction));
    }

    public Task<RoomStateResult> GetStateAsync()
        => Task.FromResult(BuildRoomState());

    public Task<RoomStateResult> SetReadyAsync(Guid snakeId, bool isReady)
    {
        var snake = _snakes.FirstOrDefault(s => s.SnakeId == snakeId);
        if (snake != null) snake.IsReady = isReady;
        return Task.FromResult(BuildRoomState());
    }

    public Task<RoomStateResult> StartGameAsync(Guid hostSnakeId)
    {
        if (hostSnakeId == _hostSnakeId && _snakes.Count > 0 && _snakes.All(s => s.IsReady))
            _gameStatus = GameStatus.Playing;
        return Task.FromResult(BuildRoomState());
    }

    public Task<RoomStateResult> UpdateSnakeAsync(Guid snakeId, UpdateSnakeData update)
    {
        if (_gameStatus != GameStatus.Playing)
            return Task.FromResult(BuildRoomState());

        var snake = _snakes.FirstOrDefault(s => s.SnakeId == snakeId);
        if (snake != null)
        {
            bool scoreIncreased = update.Score > snake.Score;
            snake.Positions = update.Positions;
            snake.Direction = update.Direction;
            snake.Score = update.Score;
            snake.IsAlive = update.IsAlive;

            if (scoreIncreased)
                snake.Food = GenerateFood(update.Positions.ToHashSet());
        }

        // Transition to GameOver when every snake is dead
        if (_gameStatus == GameStatus.Playing && _snakes.Count > 0 && _snakes.All(s => !s.IsAlive))
            _gameStatus = GameStatus.GameOver;
        return Task.FromResult(BuildRoomState());
    }

    public Task LeaveAsync(Guid snakeId)
    {
        _snakes.RemoveAll(s => s.SnakeId == snakeId);
        return Task.CompletedTask;
    }

    private RoomStateResult BuildRoomState()
        => new(
            this.GetPrimaryKeyString(),
            _tickIntervalMs,
            _isActive,
            _snakes.Select(s => new SnakeStateResult(
                s.SnakeId, s.PlayerId, s.PlayerName,
                s.Positions.ToList(), s.Direction, s.Score, s.IsAlive,
                s.IsReady, s.Food)).ToList(),
            _gameStatus,
            _hostSnakeId);

    private GrainPosition GenerateFood(HashSet<GrainPosition> occupied)
    {
        GrainPosition candidate;
        int attempts = 0;
        do
        {
            candidate = new GrainPosition(_rng.Next(GridSize), _rng.Next(GridSize));
            attempts++;
        } while (occupied.Contains(candidate) && attempts < 200);
        return candidate;
    }

    private static List<GrainPosition> MakeInitialSnake(int headX, int headY, string direction)
    {
        // Positions ordered tail-first, head-last (SnakeGameEngine.Snake.Last = head)
        var positions = new List<GrainPosition>();
        for (int i = 3; i >= 0; i--)
        {
            positions.Add(direction switch
            {
                "Right" => new GrainPosition(headX - i, headY),
                "Left"  => new GrainPosition(headX + i, headY),
                "Down"  => new GrainPosition(headX, headY - i),
                "Up"    => new GrainPosition(headX, headY + i),
                _       => new GrainPosition(headX - i, headY),
            });
        }
        return positions;
    }

    private class SnakeInfo
    {
        public Guid SnakeId { get; set; }
        public Guid PlayerId { get; set; }
        public string PlayerName { get; set; } = "";
        public List<GrainPosition> Positions { get; set; } = new();
        public string Direction { get; set; } = "Right";
        public GrainPosition Food { get; set; } = new(0, 0);
        public int Score { get; set; }
        public bool IsAlive { get; set; }
        public bool IsReady { get; set; }
    }
}

