namespace AiTravelAgent.Configuration;

public class AppSettings
{
    public string AppName { get; set; } = "HelloAgents智能旅行助手";
    public string AppVersion { get; set; } = "2.0.0";
    public bool Debug { get; set; } = false;

    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 8000;

    public string CorsOrigins { get; set; } = "http://localhost:5173,http://localhost:5174,http://localhost:5175,http://localhost:3000,http://127.0.0.1:5173,http://127.0.0.1:5174,http://127.0.0.1:5175,http://127.0.0.1:3000";

    public string AmapWebKey { get; set; } = "ebe35234350890376a4faa7f595ee7e6";
    public string AmapJsKey { get; set; } = "cb7adaf028f818a0d0f12433ac9005df";

    public string GoogleMapsApiKey { get; set; } = "";
    public string GoogleMapsProxy { get; set; } = "";

    public string XhsCookie { get; set; } = "";

    public string OpenAiApiKey { get; set; } = "ark-b66cba50-0a60-4e6c-a683-19ca4b194e46-360df"; // 豆包的OpenAI API Key 豆包Endpoint ID："model": "ep-20260507113644-njsp9"
    public string OpenAiBaseUrl { get; set; } = "https://api.openai.com/v1";
    public string OpenAiModel { get; set; } = "gpt-4";

    public string LogLevel { get; set; } = "INFO";

    public string[] GetCorsOriginsList()
    {
        return CorsOrigins.Split(',').Select(o => o.Trim()).ToArray();
    }
}
