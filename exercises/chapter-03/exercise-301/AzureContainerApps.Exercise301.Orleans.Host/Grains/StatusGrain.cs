namespace AzureContainerApps.Exercise301.Orleans.Host.Grains;

public class StatusGrain : Grain, IStatusGrain
{
    public async Task<StatusResult> GetStatusAsync()
    {
        var grainId = this.GetPrimaryKey();

        await Task.Delay(TimeSpan.FromSeconds(5));

        DeactivateOnIdle();
        return new StatusResult(Environment.MachineName, grainId);
    }
}