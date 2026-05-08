using System.Net.Http;
using System.Text;
using AiTravelAgent.Configuration;
using AiTravelAgent.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AiTravelAgent.Services;

public class GoogleMapService
{
    private static GoogleMapService? _instance;
    private static readonly object _lock = new();

    private readonly HttpClient _httpClient;
    private readonly AppSettings _settings;

    private GoogleMapService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _settings = ConfigurationLoader.LoadSettings();
    }

    public static GoogleMapService? GetInstance()
    {
        if (string.IsNullOrEmpty(ConfigurationLoader.LoadSettings().GoogleMapsApiKey))
            return null;

        if (_instance == null)
        {
            lock (_lock)
            {
                if (_instance == null)
                    _instance = new GoogleMapService();
            }
        }
        return _instance;
    }

    public async Task<List<POIInfo>> SearchPOIAsync(string keywords, string city)
    {
        var result = new List<POIInfo>();

        try
        {
            var url = "https://places.googleapis.com/v1/places:searchText";

            var requestBody = new
            {
                textQuery = $"{city} {keywords}",
                languageCode = "zh-CN"
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = content;
            request.Headers.Add("X-Goog-Api-Key", _settings.GoogleMapsApiKey);
            request.Headers.Add("X-Goog-FieldMask", "places.id,places.displayName,places.formattedAddress,places.location,places.types,places.internationalPhoneNumber");

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Google Places API Error: {response.StatusCode} - {responseContent}");
                return result;
            }

            var data = JObject.Parse(responseContent);

            foreach (var place in data["places"] ?? new JArray())
            {
                var loc = place["location"];
                result.Add(new POIInfo
                {
                    Id = place["id"]?.ToString() ?? "",
                    Name = place["displayName"]?["text"]?.ToString() ?? "",
                    Type = string.Join(",", place["types"]?.Take(3) ?? new JArray()),
                    Address = place["formattedAddress"]?.ToString() ?? "",
                    Location = loc != null
                        ? new Location
                        {
                            Longitude = double.Parse(loc["longitude"]?.ToString() ?? "0"),
                            Latitude = double.Parse(loc["latitude"]?.ToString() ?? "0")
                        }
                        : new Location(),
                    Tel = place["internationalPhoneNumber"]?.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Google Places Search Error: {ex.Message}");
        }

        return result;
    }

    public async Task<Location?> GeocodeAsync(string address, string? city = null)
    {
        try
        {
            var fullAddress = city != null ? $"{address}, {city}" : address;
            var url = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(fullAddress)}&key={_settings.GoogleMapsApiKey}&language=zh-CN";

            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            var data = JObject.Parse(content);

            var results = data["results"] as JArray;
            if (results != null && results.HasValues)
            {
                var loc = results[0]["geometry"]?["location"];
                if (loc != null)
                {
                    return new Location
                    {
                        Longitude = double.Parse(loc["lng"]?.ToString() ?? "0"),
                        Latitude = double.Parse(loc["lat"]?.ToString() ?? "0")
                    };
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Google Geocode Error: {ex.Message}");
        }

        return null;
    }

    public async Task<List<WeatherInfo>> GetWeatherAsync(string city)
    {
        var result = new List<WeatherInfo>();

        try
        {
            var loc = await GeocodeAsync(city);
            if (loc == null)
            {
                Console.WriteLine($"Google Weather: Cannot geocode city '{city}'");
                return result;
            }

            var url = $"https://weather.googleapis.com/v1/forecast/days:lookup?key={_settings.GoogleMapsApiKey}&location.latitude={loc.Latitude}&location.longitude={loc.Longitude}&days=7&languageCode=zh-CN&unitsSystem=METRIC";

            var response = await _httpClient.GetAsync(url);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Google Weather API Error: {response.StatusCode}");
                return result;
            }

            var data = JObject.Parse(responseContent);

            var conditionMap = new Dictionary<string, string>
            {
                { "CLEAR", "晴" }, { "MOSTLY_CLEAR", "晴" },
                { "PARTLY_CLOUDY", "多云" }, { "MOSTLY_CLOUDY", "多云" },
                { "CLOUDY", "阴" }, { "OVERCAST", "阴" },
                { "LIGHT_RAIN", "小雨" }, { "RAIN", "中雨" },
                { "MODERATE_RAIN", "中雨" }, { "HEAVY_RAIN", "大雨" },
                { "LIGHT_SNOW", "小雪" }, { "SNOW", "中雪" },
                { "HEAVY_SNOW", "大雪" }, { "THUNDERSTORM", "雷阵雨" },
                { "DRIZZLE", "毛毛雨" }, { "FOG", "雾" },
                { "HAZE", "霾" }, { "WIND", "大风" }
            };

            foreach (var dayData in data["forecastDays"] ?? new JArray())
            {
                var dateInfo = dayData["displayDate"];
                var year = dateInfo?["year"]?.ToString() ?? "2025";
                var month = dateInfo?["month"]?.ToString() ?? "1";
                var day = dateInfo?["day"]?.ToString() ?? "1";
                var dateStr = $"{year}-{int.Parse(month):D2}-{int.Parse(day):D2}";

                var daytime = dayData["daytimeForecast"];
                var nighttime = dayData["nighttimeForecast"];
                var dayWind = daytime?["wind"];
                var windSpeed = dayWind?["wind"]?["speed"]?["value"]?.ToString() ?? "0";

                double.TryParse(windSpeed, out var speed);
                var windPower = speed < 6 ? "微风" : speed < 12 ? "1-2级" : speed < 20 ? "3级" : speed < 29 ? "4级" : speed < 39 ? "5级" : "6级以上";

                var dayCondition = daytime?["weatherCondition"]?.ToString() ?? "";
                var nightCondition = nighttime?["weatherCondition"]?.ToString() ?? "";

                result.Add(new WeatherInfo
                {
                    Date = dateStr,
                    DayWeather = conditionMap.GetValueOrDefault(dayCondition, dayCondition),
                    NightWeather = conditionMap.GetValueOrDefault(nightCondition, nightCondition),
                    DayTemp = (int)(dayData["maxTemperature"]?["degrees"]?.Value<double>() ?? 0),
                    NightTemp = (int)(dayData["minTemperature"]?["degrees"]?.Value<double>() ?? 0),
                    WindDirection = dayWind?["wind"]?["direction"]?["cardinal"]?.ToString() ?? "",
                    WindPower = windPower
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Google Weather Error: {ex.Message}");
        }

        return result;
    }
}
