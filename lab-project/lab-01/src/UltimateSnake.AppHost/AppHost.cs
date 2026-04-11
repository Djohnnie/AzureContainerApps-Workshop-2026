var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.UltimateSnake_Frontend>("frontend")
    .WithHttpEndpoint();

builder.Build().Run();
