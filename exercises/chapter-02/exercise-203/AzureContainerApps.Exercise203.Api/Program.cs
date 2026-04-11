var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/status", () => new { Server = Environment.MachineName });

app.Run();
