namespace AzureContainerApps.Exercise401.Orleans.Host.Grains;

public class StatusGrain : Grain, IStatusGrain
{
    public async Task<StatusResult> GetStatusAsync()
    {
        var grainId = this.GetPrimaryKey();

        await Task.Delay(TimeSpan.FromMinutes(1));

        DeactivateOnIdle();
        return new StatusResult(Environment.MachineName, grainId);
    }
}