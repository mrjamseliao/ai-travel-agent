using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AiTravelAgent.Configuration;

public static class ConfigurationLoader
{
    private static AppSettings? _settings;
    private static readonly object _lock = new();
    private static readonly string SettingsFile = "appsettings.json";
    private static readonly string EnvFile = ".env";

    public static AppSettings LoadSettings()
    {
        if (_settings != null)
            return _settings;

        lock (_lock)
        {
            if (_settings != null)
                return _settings;

            LoadEnvFile();
            
            var envVars = Environment.GetEnvironmentVariables();

            _settings = new AppSettings
            {
                AppName = envVars["APP_NAME"]?.ToString() ?? "HelloAgents智能旅行助手",
                AppVersion = envVars["APP_VERSION"]?.ToString() ?? "2.0.0",
                Debug = bool.TryParse(envVars["DEBUG"]?.ToString(), out var debug) && debug,
                Host = envVars["HOST"]?.ToString() ?? "0.0.0.0",
                Port = int.TryParse(envVars["PORT"]?.ToString(), out var port) ? port : 8000,
                CorsOrigins = envVars["CORS_ORIGINS"]?.ToString() ?? "http://localhost:5173,http://localhost:5174,http://localhost:5175,http://localhost:3000,http://127.0.0.1:5173,http://127.0.0.1:5174,http://127.0.0.1:5175,http://127.0.0.1:3000",
                AmapWebKey = envVars["AMAP_WEB_KEY"]?.ToString() ?? "",
                AmapJsKey = envVars["AMAP_JS_KEY"]?.ToString() ?? "",
                GoogleMapsApiKey = envVars["GOOGLE_MAPS_API_KEY"]?.ToString() ?? "",
                GoogleMapsProxy = envVars["GOOGLE_MAPS_PROXY"]?.ToString() ?? "",
                XhsCookie = envVars["XHS_COOKIE"]?.ToString() ?? "",
                OpenAiApiKey = envVars["OPENAI_API_KEY"]?.ToString() ?? envVars["LLM_API_KEY"]?.ToString() ?? "",
                OpenAiBaseUrl = envVars["OPENAI_BASE_URL"]?.ToString() ?? envVars["LLM_BASE_URL"]?.ToString() ?? "https://api.openai.com/v1",
                OpenAiModel = envVars["OPENAI_MODEL"]?.ToString() ?? envVars["LLM_MODEL_ID"]?.ToString() ?? "gpt-4",
                LogLevel = envVars["LOG_LEVEL"]?.ToString() ?? "INFO",
                DbProvider = envVars["DB_PROVIDER"]?.ToString() ?? "pgsql",
                DbType = envVars["DB_TYPE"]?.ToString() ?? "pgsql",
                DbHost = envVars["DB_HOST"]?.ToString() ?? "localhost",
                DbPort = int.TryParse(envVars["DB_PORT"]?.ToString(), out var dbPort) ? dbPort : 5432,
                DbName = envVars["DB_NAME"]?.ToString() ?? "ai_travel_agent",
                DbAccount = envVars["DB_ACCOUNT"]?.ToString() ?? "postgres",
                DbPassword = envVars["DB_PASSWORD"]?.ToString() ?? ""
            };

            if (File.Exists(SettingsFile))
            {
                try
                {
                    var json = File.ReadAllText(SettingsFile);
                    var fileSettings = JsonConvert.DeserializeObject<JObject>(json);
                    if (fileSettings != null)
                    {
                        foreach (var kvp in fileSettings)
                        {
                            var prop = typeof(AppSettings).GetProperty(kvp.Key);
                            if (prop != null && kvp.Value != null)
                            {
                                var value = kvp.Value is JValue jValue ? jValue.Value : kvp.Value;
                                prop.SetValue(_settings, Convert.ChangeType(value, prop.PropertyType));
                            }
                        }

                        LoadDatabaseSettings(fileSettings);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to load settings from file: {ex.Message}");
                }
            }

            return _settings;
        }
    }

    private static void LoadDatabaseSettings(JObject fileSettings)
    {
        var dbSection = fileSettings["database"] as JObject;
        if (dbSection == null)
            return;

        var providerSection = dbSection["provider"] as JObject;
        if (providerSection != null)
        {
            if (providerSection.TryGetValue("name", out var providerName))
                _settings!.DbProvider = providerName.ToString() ?? "pgsql";
            if (providerSection.TryGetValue("type", out var providerType))
                _settings!.DbType = providerType.ToString() ?? "pgsql";
        }

        var connectionSection = dbSection["connection"] as JObject;
        if (connectionSection != null)
        {
            if (connectionSection.TryGetValue("type", out var connType))
                _settings!.DbType = connType.ToString() ?? "pgsql";
            if (connectionSection.TryGetValue("host", out var host))
                _settings!.DbHost = host.ToString() ?? "localhost";
            if (connectionSection.TryGetValue("port", out var port))
                _settings!.DbPort = int.TryParse(port.ToString(), out var p) ? p : 5432;
            if (connectionSection.TryGetValue("name", out var name))
                _settings!.DbName = name.ToString() ?? "ai_travel_agent";
            if (connectionSection.TryGetValue("account", out var account))
                _settings!.DbAccount = account.ToString() ?? "postgres";
            if (connectionSection.TryGetValue("password", out var password))
                _settings!.DbPassword = password.ToString() ?? "";
        }
    }

    public static void ReloadSettings()
    {
        _settings = null;
    }

    private static void LoadEnvFile()
    {
        if (!File.Exists(EnvFile))
            return;

        try
        {
            var lines = File.ReadAllLines(EnvFile);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    continue;

                var parts = trimmed.Split('=', 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();
                    
                    if (!string.IsNullOrEmpty(key) && !Environment.GetEnvironmentVariables().Contains(key))
                    {
                        Environment.SetEnvironmentVariable(key, value);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to load .env file: {ex.Message}");
        }
    }
}
