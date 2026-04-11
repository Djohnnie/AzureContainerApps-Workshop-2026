namespace UltimateSnake.Orleans.Contracts;

public interface IPlayerGrain : IGrainWithGuidKey
{
    Task<PlayerInfo> GetInfoAsync();
    Task InitAsync(string name);
}
