namespace UltimateSnake.Frontend.Client.Models;

public enum Direction { Up, Down, Left, Right }

public enum GamePhase { NameEntry, Countdown, Playing, GameOver }

public readonly record struct Position(int X, int Y);
