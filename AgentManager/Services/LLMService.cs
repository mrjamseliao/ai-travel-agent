using System.Net.Http.Headers;
using System.Text;
using AiTravelAgent.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AiTravelAgent.Services;

public class LLMService
{
    private static LLMService? _instance;
    private static readonly object _lock = new();

    private readonly HttpClient _httpClient;
    private readonly AppSettings _settings;
    private string _model;
    private string _apiKey;
    private string _baseUrl;
    private bool _isVolces;

    private LLMService()
    {
        _settings = ConfigurationLoader.LoadSettings();
        _apiKey = _settings.OpenAiApiKey ?? "";
        _baseUrl = _settings.OpenAiBaseUrl ?? "https://api.openai.com/v1";
        _model = _settings.OpenAiModel ?? "gpt-4";
        _isVolces = _baseUrl.Contains("volces.com");

        _httpClient = new HttpClient();
        
        if (_isVolces)
        {
            _httpClient.DefaultRequestHeaders.Add("X-Ark-Api-Key", _apiKey);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    public static LLMService GetInstance()
    {
        if (_instance == null)
        {
            lock (_lock)
            {
                _instance ??= new LLMService();
            }
        }
        return _instance;
    }

    public void Reset()
    {
        _instance = null;
    }

    public async Task<string> ChatCompletionAsync(string systemPrompt, string userMessage, int maxTokens = 2048, double temperature = 0.7)
    {
        var messages = new[]
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = userMessage }
        };

        var requestBody = new
        {
            model = _model,
            messages = messages,
            temperature = temperature,
            max_tokens = maxTokens
        };

        var json = JsonConvert.SerializeObject(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        string url = $"{_baseUrl.TrimEnd('/')}/chat/completions";

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"LLM API Response: {responseContent.Substring(0, Math.Min(1000, responseContent.Length))}...");

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"LLM API Error: {response.StatusCode} - {responseContent}");
                return $"Error: {response.StatusCode}";
            }

            var result = JObject.Parse(responseContent);
            var reply = result["choices"]?[0]?["message"]?["content"]?.ToString() ?? 
                       result["result"]?.ToString() ?? 
                       result["content"]?.ToString() ?? "";
            return reply;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LLM API Exception: {ex.Message}");
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> ChatCompletionWithHistoryAsync(List<(string role, string content)> messages, int maxTokens = 2048, double temperature = 0.7)
    {
        var requestMessages = messages.Select(m => new { role = m.role, content = m.content }).ToList();

        var requestBody = new
        {
            model = _model,
            messages = requestMessages,
            temperature = temperature,
            max_tokens = maxTokens
        };

        var json = JsonConvert.SerializeObject(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        string url = $"{_baseUrl.TrimEnd('/')}/chat/completions";

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"LLM API Error: {response.StatusCode} - {responseContent}");
                return $"Error: {response.StatusCode}";
            }

            var result = JObject.Parse(responseContent);
            var reply = result["choices"]?[0]?["message"]?["content"]?.ToString() ?? 
                       result["result"]?.ToString() ?? 
                       result["content"]?.ToString() ?? "";
            return reply;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LLM API Exception: {ex.Message}");
            return $"Error: {ex.Message}";
        }
    }
}
