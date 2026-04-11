var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.UltimateSnake_Backend_Api>("api")
    .WithHttpEndpoint();

builder.AddProject<Projects.UltimateSnake_Frontend>("frontend")
    .WithHttpEndpoint()
    .WithReference(api)
    .WithEnvironment("API_BASE_URL", api.GetEndpoint("http"));

builder.Build().Run();
