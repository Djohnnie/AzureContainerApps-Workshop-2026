using UltimateSnake.Frontend.Client.Models;

namespace UltimateSnake.Frontend.Client.Services;

public class SnakeGameEngine
{
    public const int GridSize = 30;

    public LinkedList<Position> Snake { get; } = new();
    public Position Food { get; private set; }
    public Direction CurrentDirection { get; private set; }
    public int Score { get; private set; }
    public bool IsAlive { get; private set; }

    private Direction _nextDirection;
    private readonly Random _rng = new();
    private readonly HashSet<Position> _snakeSet = new();

    public void Initialize()
    {
        Snake.Clear();
        _snakeSet.Clear();
        Score = 0;
        IsAlive = true;
        CurrentDirection = Direction.Right;
        _nextDirection = Direction.Right;

        int startX = GridSize / 2 - 1;
        int startY = GridSize / 2;
        for (int i = 3; i >= 0; i--)
        {
            var pos = new Position(startX - i, startY);
            Snake.AddLast(pos);
            _snakeSet.Add(pos);
        }

        SpawnFood();
    }

    public void InitializeFromServer(List<Position> positions, Direction direction, Position food)
    {
        Snake.Clear();
        _snakeSet.Clear();
        Score = 0;
        IsAlive = true;
        CurrentDirection = direction;
        _nextDirection = direction;

        foreach (var pos in positions)
        {
            Snake.AddLast(pos);
            _snakeSet.Add(pos);
        }

        Food = food;
    }

    public void SetFood(Position food) => Food = food;

    public void SetNextDirection(Direction d)
    {
        if ((CurrentDirection == Direction.Up    && d == Direction.Down)  ||
            (CurrentDirection == Direction.Down  && d == Direction.Up)    ||
            (CurrentDirection == Direction.Left  && d == Direction.Right) ||
            (CurrentDirection == Direction.Right && d == Direction.Left))
            return;

        _nextDirection = d;
    }

    /// <summary>Advances the game by one tick. Returns true while alive.</summary>
    public bool Tick()
    {
        if (!IsAlive) return false;

        CurrentDirection = _nextDirection;

        var head = Snake.Last!.Value;
        var next = CurrentDirection switch
        {
            Direction.Up    => new Position(head.X, (head.Y - 1 + GridSize) % GridSize),
            Direction.Down  => new Position(head.X, (head.Y + 1) % GridSize),
            Direction.Left  => new Position((head.X - 1 + GridSize) % GridSize, head.Y),
            Direction.Right => new Position((head.X + 1) % GridSize, head.Y),
            _               => head
        };

        bool ateFood = next == Food;

        if (!ateFood)
        {
            var tail = Snake.First!.Value;
            Snake.RemoveFirst();
            _snakeSet.Remove(tail);
        }

        if (_snakeSet.Contains(next))
        {
            IsAlive = false;
            return false;
        }

        Snake.AddLast(next);
        _snakeSet.Add(next);

        if (ateFood)
        {
            Score++;
            SpawnFood();
        }

        return true;
    }

    public CellType GetCell(int x, int y)
    {
        var pos = new Position(x, y);
        if (pos == Food) return CellType.Food;
        if (Snake.Last?.Value == pos) return CellType.SnakeHead;
        if (_snakeSet.Contains(pos)) return CellType.SnakeBody;
        return CellType.Empty;
    }

    private void SpawnFood()
    {
        Position candidate;
        do
        {
            candidate = new Position(_rng.Next(GridSize), _rng.Next(GridSize));
        } while (_snakeSet.Contains(candidate));
        Food = candidate;
    }
}

public enum CellType { Empty, SnakeHead, SnakeBody, OtherSnakeHead, OtherSnakeBody, Food }
