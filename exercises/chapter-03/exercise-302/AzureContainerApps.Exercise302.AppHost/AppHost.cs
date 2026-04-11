var builder = DistributedApplication.CreateBuilder(args);

var orleansHost = builder.AddProject<Projects.AzureContainerApps_Exercise302_Orleans_Host>("orleans-host")
    .WithUrl("/dashboard", "Orleans Dashboard");

var api = builder.AddProject<Projects.AzureContainerApps_Exercise302_Api>("api")
    .WaitFor(orleansHost)
    .WithUrlForEndpoint("https", url => url.DisplayText = "api");

builder.AddProject<Projects.AzureContainerApps_Exercise302_Worker>("worker")
    .WaitFor(api)
    .WithEnvironment("API_BASE_URL", api.GetEndpoint("https"));

builder.Build().Run();