using UltimateSnake.Frontend.Components;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddMudServices();

var apiBaseUrl = builder.Configuration["API_BASE_URL"] ?? "http://localhost:8081";
builder.Services.AddHttpClient("backend", c => c.BaseAddress = new Uri(apiBaseUrl));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();

app.MapGet("/api/server-name", async (IHttpClientFactory factory) =>
{
    try
    {
        var client = factory.CreateClient("backend");
        var result = await client.GetFromJsonAsync<ServerNameResponse>("/server-name");
        return Results.Ok(result);
    }
    catch
    {
        return Results.Ok(new ServerNameResponse("unavailable"));
    }
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(UltimateSnake.Frontend.Client._Imports).Assembly);

app.Run();

record ServerNameResponse(string ServerName);
