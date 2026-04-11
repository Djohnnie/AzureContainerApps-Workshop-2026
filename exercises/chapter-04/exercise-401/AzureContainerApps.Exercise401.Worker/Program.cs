using System.Net.Http.Json;

var builder = Host.CreateApplicationBuilder(args);

#if ASPIRE
builder.AddServiceDefaults();
#endif

var apiBaseUrl = builder.Configuration["API_BASE_URL"]
    ?? throw new InvalidOperationException("API_BASE_URL environment variable is not set.");

builder.Services.AddHttpClient("api", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});
builder.Services.AddHostedService<LoadGeneratorService>();

await builder.Build().RunAsync();

public class LoadGeneratorService(IHttpClientFactory httpClientFactory, ILogger<LoadGeneratorService> logger) : BackgroundService
{
    private readonly HttpClient _client = httpClientFactory.CreateClient("api");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Load generator started. Target: {BaseAddress}", _client.BaseAddress);

        while (!stoppingToken.IsCancellationRequested)
        {
            var tasks = Enumerable.Range(0, 50).Select(async _ =>
            {
                try
                {
                    var result = await _client.GetFromJsonAsync<StatusResponse>("/status", stoppingToken);
                    if (result is not null)
                        logger.LogInformation("MachineName: {MachineName}, GrainId: {GrainId}", result.MachineName, result.GrainId);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    logger.LogWarning("Request failed: {Message}", ex.Message);
                }
            });

            await Task.WhenAll(tasks);
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
    }
}

record StatusResponse(string MachineName, Guid GrainId);
