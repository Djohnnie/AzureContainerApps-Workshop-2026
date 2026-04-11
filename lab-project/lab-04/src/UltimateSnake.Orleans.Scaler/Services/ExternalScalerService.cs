using Externalscaler;
using Grpc.Core;

namespace UltimateSnake.Orleans.Scaler.Services;

public class ExternalScalerService : ExternalScaler.ExternalScalerBase
{
    private readonly IManagementGrain _managementGrain;
    private readonly ILogger<ExternalScalerService> _logger;
    private const string MetricName = "siloThreshold";

    public ExternalScalerService(IClusterClient clusterClient, ILogger<ExternalScalerService> logger)
    {
        _logger = logger;
        _managementGrain = clusterClient.GetGrain<IManagementGrain>(0);
        _logger.LogInformation("ExternalScalerService created.");
    }

    public override async Task<GetMetricsResponse> GetMetrics(GetMetricsRequest request, ServerCallContext context)
    {
        _logger.LogInformation("GetMetrics called.");

        CheckRequestMetadata(request.ScaledObjectRef);

        var upperbound = Convert.ToInt32(request.ScaledObjectRef.ScalerMetadata["upperbound"]);
        var (grainCount, siloCount) = await GetClusterStats();

        long grainsPerSilo = (grainCount > 0 && siloCount > 0) ? (grainCount / siloCount) : 0;
        long metricValue = siloCount;

        if (grainsPerSilo < upperbound)
            metricValue = grainCount == 0 ? 1 : Math.Max(1, grainCount / upperbound);

        if (grainsPerSilo >= upperbound)
            metricValue = siloCount + 1;

        if (metricValue == 0)
            metricValue = 1;

        _logger.LogInformation(
            "GrainsPerSilo: {GrainsPerSilo}, UpperBound: {UpperBound}, GrainCount: {GrainCount}, SiloCount: {SiloCount}. Scale to {MetricValue}.",
            grainsPerSilo, upperbound, grainCount, siloCount, metricValue);

        var response = new GetMetricsResponse();
        response.MetricValues.Add(new MetricValue
        {
            MetricName = MetricName,
            MetricValue_ = metricValue
        });

        return response;
    }

    public override Task<GetMetricSpecResponse> GetMetricSpec(ScaledObjectRef request, ServerCallContext context)
    {
        _logger.LogInformation("GetMetricSpec called.");

        CheckRequestMetadata(request);

        var resp = new GetMetricSpecResponse();
        resp.MetricSpecs.Add(new MetricSpec
        {
            MetricName = MetricName,
            TargetSize = 1
        });

        return Task.FromResult(resp);
    }

    public override async Task<IsActiveResponse> IsActive(ScaledObjectRef request, ServerCallContext context)
    {
        _logger.LogInformation("IsActive called.");

        CheckRequestMetadata(request);

        var result = await IsScaleOutRequired(request);
        _logger.LogInformation("IsActive returning {Result}.", result);

        return new IsActiveResponse { Result = result };
    }

    public override async Task StreamIsActive(ScaledObjectRef request, IServerStreamWriter<IsActiveResponse> responseStream, ServerCallContext context)
    {
        _logger.LogInformation("StreamIsActive called.");

        CheckRequestMetadata(request);

        while (!context.CancellationToken.IsCancellationRequested)
        {
            if (await IsScaleOutRequired(request))
            {
                await responseStream.WriteAsync(new IsActiveResponse { Result = true });
            }

            await Task.Delay(TimeSpan.FromSeconds(30), context.CancellationToken);
        }
    }

    private static void CheckRequestMetadata(ScaledObjectRef request)
    {
        if (!request.ScalerMetadata.ContainsKey("upperbound"))
            throw new ArgumentException("'upperbound' must be specified in scaler metadata.");
    }

    private async Task<bool> IsScaleOutRequired(ScaledObjectRef request)
    {
        var upperbound = Convert.ToInt32(request.ScalerMetadata["upperbound"]);
        var (grainCount, siloCount) = await GetClusterStats();

        if (grainCount == 0 || siloCount == 0) return false;

        return (grainCount / siloCount) >= upperbound;
    }

    private async Task<(long GrainCount, long SiloCount)> GetClusterStats()
    {
        var statistics = await _managementGrain.GetDetailedGrainStatistics();
        var grainCount = (long)statistics.Length;

        var hosts = await _managementGrain.GetDetailedHosts();
        var siloCount = (long)hosts.Count(x => x.Status == SiloStatus.Active);

        _logger.LogInformation("Cluster stats: {GrainCount} grains, {SiloCount} active silos.", grainCount, siloCount);

        return (grainCount, siloCount);
    }
}
