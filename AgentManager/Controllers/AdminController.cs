using AiTravelAgent.Configuration;
using AiTravelAgent.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace AiTravelAgent.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    [HttpGet("settings")]
    public IActionResult GetSettings()
    {
        var settings = ConfigurationLoader.LoadSettings();
        return Ok(new
        {
            success = true,
            data = new
            {
                appName = settings.AppName,
                appVersion = settings.AppVersion,
                debug = settings.Debug,
                host = settings.Host,
                port = settings.Port,
                corsOrigins = settings.CorsOrigins,
                hasAmapKey = !string.IsNullOrEmpty(settings.AmapWebKey),
                hasGoogleMapsKey = !string.IsNullOrEmpty(settings.GoogleMapsApiKey),
                hasOpenAiKey = !string.IsNullOrEmpty(settings.OpenAiApiKey),
                hasXhsCookie = !string.IsNullOrEmpty(settings.XhsCookie),
                openAiBaseUrl = settings.OpenAiBaseUrl,
                openAiModel = settings.OpenAiModel,
                logLevel = settings.LogLevel,
                vite_amap_web_key = settings.AmapWebKey,
                vite_amap_web_js_key = settings.AmapJsKey,
                google_maps_api_key = settings.GoogleMapsApiKey,
                google_maps_proxy = settings.GoogleMapsProxy,
                xhs_cookie = settings.XhsCookie,
                openai_api_key = settings.OpenAiApiKey
            }
        });
    }

    [HttpPost("settings")]
    public IActionResult UpdateSettings([FromBody] Dictionary<string, object> updates)
    {
        try
        {
            var envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
            var envLines = new List<string>();

            if (System.IO.File.Exists(envFilePath))
            {
                envLines = System.IO.File.ReadAllLines(envFilePath).ToList();
            }

            foreach (var kvp in updates)
            {
                var envKey = GetEnvKeyName(kvp.Key);
                var value = kvp.Value?.ToString() ?? "";

                var existingIndex = envLines.FindIndex(line => 
                    line.StartsWith(envKey + "=", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith(envKey + " ", StringComparison.OrdinalIgnoreCase));

                var newLine = $"{envKey}={value}";

                if (existingIndex >= 0)
                {
                    envLines[existingIndex] = newLine;
                }
                else
                {
                    envLines.Add(newLine);
                }
            }

            System.IO.File.WriteAllLines(envFilePath, envLines);

            ConfigurationLoader.ReloadSettings();

            return Ok(new
            {
                success = true,
                message = "设置已更新，部分设置需要重启服务才能生效"
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                success = false,
                message = $"更新设置失败: {ex.Message}"
            });
        }
    }

    private string GetEnvKeyName(string settingName)
    {
        return settingName switch
        {
            "openAiApiKey" => "OPENAI_API_KEY",
            "openAiBaseUrl" => "OPENAI_BASE_URL",
            "openAiModel" => "OPENAI_MODEL",
            "amapWebKey" => "AMAP_WEB_KEY",
            "amapJsKey" => "AMAP_JS_KEY",
            "googleMapsApiKey" => "GOOGLE_MAPS_API_KEY",
            "googleMapsProxy" => "GOOGLE_MAPS_PROXY",
            "xhsCookie" => "XHS_COOKIE",
            _ => settingName.ToUpper()
        };
    }

    [HttpGet("health/details")]
    public IActionResult GetHealthDetails()
    {
        var settings = ConfigurationLoader.LoadSettings();
        var checks = new List<object>();

        checks.Add(new
        {
            name = "应用服务",
            status = "healthy",
            message = "服务运行正常"
        });

        checks.Add(new
        {
            name = "LLM服务",
            status = !string.IsNullOrEmpty(settings.OpenAiApiKey) ? "healthy" : "warning",
            message = !string.IsNullOrEmpty(settings.OpenAiApiKey) ? "API Key已配置" : "未配置LLM API Key"
        });

        checks.Add(new
        {
            name = "高德地图",
            status = !string.IsNullOrEmpty(settings.AmapWebKey) ? "healthy" : "warning",
            message = !string.IsNullOrEmpty(settings.AmapWebKey) ? "API Key已配置" : "未配置高德地图API Key"
        });

        checks.Add(new
        {
            name = "Google Maps",
            status = !string.IsNullOrEmpty(settings.GoogleMapsApiKey) ? "healthy" : "warning",
            message = !string.IsNullOrEmpty(settings.GoogleMapsApiKey) ? "API Key已配置" : "未配置Google Maps API Key"
        });

        checks.Add(new
        {
            name = "小红书",
            status = !string.IsNullOrEmpty(settings.XhsCookie) ? "healthy" : "warning",
            message = !string.IsNullOrEmpty(settings.XhsCookie) ? "Cookie已配置" : "未配置小红书Cookie"
        });

        var overallStatus = checks.All(c => ((string)c.GetType().GetProperty("status")?.GetValue(c) ?? "healthy") == "healthy") 
            ? "healthy" 
            : "degraded";

        return Ok(new
        {
            success = true,
            overallStatus = overallStatus,
            checks = checks,
            timestamp = DateTime.UtcNow.ToString("o"),
            uptime = GetUptime()
        });
    }

    private string GetUptime()
    {
        var startTime = DateTime.Now - Process.GetCurrentProcess().StartTime;
        return $"{startTime.Days}天 {startTime.Hours}小时 {startTime.Minutes}分钟 {startTime.Seconds}秒";
    }
}
