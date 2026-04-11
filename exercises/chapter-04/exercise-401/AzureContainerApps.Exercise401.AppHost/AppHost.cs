var builder = DistributedApplication.CreateBuilder(args);

var orleansHost = builder.AddProject<Projects.AzureContainerApps_Exercise401_Orleans_Host>("orleans-host")
    .WithUrl("/dashboard", "Orleans Dashboard");

var api = builder.AddProject<Projects.AzureContainerApps_Exercise401_Api>("api")
    .WaitFor(orleansHost)
    .WithUrlForEndpoint("https", url => url.DisplayText = "api");

builder.AddProject<Projects.AzureContainerApps_Exercise401_Worker>("worker")
    .WaitFor(api)
    .WithEnvironment("API_BASE_URL", api.GetEndpoint("https"));

builder.Build().Run();