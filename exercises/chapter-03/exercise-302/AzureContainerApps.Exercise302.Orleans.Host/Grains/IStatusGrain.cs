namespace AzureContainerApps.Exercise302.Orleans.Host.Grains;

[GenerateSerializer]
public record StatusResult([property: Id(0)] string MachineName, [property: Id(1)] Guid GrainId);

public interface IStatusGrain : IGrainWithGuidKey
{
    Task<StatusResult> GetStatusAsync();
}
