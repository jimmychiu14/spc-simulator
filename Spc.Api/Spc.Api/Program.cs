var builder = WebApplication.CreateBuilder(args);

// Use port from environment (for cloud deployment)
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();
