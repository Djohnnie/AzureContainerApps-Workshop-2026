using System.Net.Http.Headers;
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

// Server-name proxy (for dashboard display)
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

// Game API proxy – forward all /api/* calls to backend
app.Map("/api/{**path}", async (HttpContext ctx, IHttpClientFactory factory) =>
{
    var client = factory.CreateClient("backend");
    var path = ctx.Request.Path.ToString();
    var query = ctx.Request.QueryString.ToString();
    var requestUri = path + query;

    using var request = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), requestUri);

    if (ctx.Request.ContentLength > 0 || ctx.Request.ContentType != null)
    {
        request.Content = new StreamContent(ctx.Request.Body);
        if (ctx.Request.ContentType != null)
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(ctx.Request.ContentType);
    }

    try
    {
        using var response = await client.SendAsync(request);
        ctx.Response.StatusCode = (int)response.StatusCode;
        ctx.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
        await response.Content.CopyToAsync(ctx.Response.Body);
    }
    catch
    {
        ctx.Response.StatusCode = 503;
    }
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(UltimateSnake.Frontend.Client._Imports).Assembly);

app.Run();

record ServerNameResponse(string ServerName);
