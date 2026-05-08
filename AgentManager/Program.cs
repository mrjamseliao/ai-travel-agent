using AiTravelAgent.Configuration;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var settings = ConfigurationLoader.LoadSettings();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins(settings.GetCorsOriginsList())
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

var app = builder.Build();

Console.WriteLine("\n" + new string('=', 60));
Console.WriteLine($"{settings.AppName} v{settings.AppVersion}");
Console.WriteLine(new string('=', 60));

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => new
{
    name = settings.AppName,
    version = settings.AppVersion,
    status = "running"
});

app.MapGet("/health", () => new
{
    status = "healthy",
    service = settings.AppName,
    version = settings.AppVersion
});

Console.WriteLine("\n" + new string('=', 60));
Console.WriteLine($"Health Check: http://localhost:{settings.Port}/health");
Console.WriteLine(new string('=', 60) + "\n");

app.Run($"http://{settings.Host}:{settings.Port}");
