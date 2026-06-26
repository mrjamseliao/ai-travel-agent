using AiTravelAgent.Configuration;
using AiTravelAgent.Services;
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

builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(sp => DatabaseService.GetInstance(sp.GetRequiredService<AppSettings>()));
builder.Services.AddSingleton<DatabaseInitService>();

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

Console.WriteLine("\n" + new string('-', 60));
Console.WriteLine("数据库连接信息:");
Console.WriteLine($"  Provider: {settings.DbProvider}");
Console.WriteLine($"  Type: {settings.DbType}");
Console.WriteLine($"  Host: {settings.DbHost}:{settings.DbPort}");
Console.WriteLine($"  Database: {settings.DbName}");
Console.WriteLine($"  Account: {settings.DbAccount}");
Console.WriteLine(new string('-', 60));

try
{
    var dbService = DatabaseService.GetInstance(settings);
    var dbConnected = await dbService.TestConnectionAsync();
    if (dbConnected)
    {
        Console.WriteLine("✅ 数据库连接成功");
        var initService = app.Services.GetRequiredService<DatabaseInitService>();
        await initService.InitializeAsync();
    }
    else
    {
        Console.WriteLine("⚠️  数据库连接失败，服务将继续运行但部分功能不可用");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️  数据库连接测试异常: {ex.Message}");
    Console.WriteLine("服务将继续运行但部分功能不可用");
}

app.UseCors("AllowAll");

app.UseAuthorization();

app.UseStaticFiles();

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
