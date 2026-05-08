using Newtonsoft.Json;

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
                LogLevel = envVars["LOG_LEVEL"]?.ToString() ?? "INFO"
            };

            if (File.Exists(SettingsFile))
            {
                try
                {
                    var json = File.ReadAllText(SettingsFile);
                    var fileSettings = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                    if (fileSettings != null)
                    {
                        foreach (var kvp in fileSettings)
                        {
                            var prop = typeof(AppSettings).GetProperty(kvp.Key);
                            if (prop != null && kvp.Value != null)
                            {
                                var value = kvp.Value is Newtonsoft.Json.Linq.JValue jValue ? jValue.Value : kvp.Value;
                                prop.SetValue(_settings, Convert.ChangeType(value, prop.PropertyType));
                            }
                        }
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
