using Banner;

var builder = WebApplication.CreateBuilder(args);

builder.AddBanner();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();