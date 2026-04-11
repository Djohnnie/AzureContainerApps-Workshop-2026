var builder = DistributedApplication.CreateBuilder(args);

var orleansHost = builder.AddProject<Projects.UltimateSnake_Orleans_Host>("orleans-host")
    .WithEnvironment("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", "")
    .WithUrl("/dashboard", "Orleans Dashboard");

var api = builder.AddProject<Projects.UltimateSnake_Backend_Api>("api")
    .WaitFor(orleansHost)
    .WithEnvironment("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", "")
    .WithUrlForEndpoint("https", url => url.DisplayText = "api");

builder.AddProject<Projects.UltimateSnake_Frontend>("frontend")
    .WaitFor(api)
    .WithEnvironment("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", "")
    .WithEnvironment("API_BASE_URL", api.GetEndpoint("https"))
    .WithUrlForEndpoint("https", url => url.DisplayText = "frontend");

builder.Build().Run();
