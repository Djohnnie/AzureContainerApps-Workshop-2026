var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/server-name", () => new { ServerName = Environment.MachineName });

app.Run();
