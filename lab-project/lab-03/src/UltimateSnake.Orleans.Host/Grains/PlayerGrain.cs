using UltimateSnake.Orleans.Contracts;

namespace UltimateSnake.Orleans.Host.Grains;

public class PlayerGrain : Grain, IPlayerGrain
{
    private string _name = "";

    public Task<PlayerInfo> GetInfoAsync()
        => Task.FromResult(new PlayerInfo(this.GetPrimaryKey(), _name));

    public Task InitAsync(string name)
    {
        _name = name;
        return Task.CompletedTask;
    }
}
