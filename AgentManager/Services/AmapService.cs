using System.Net.Http;
using System.Web;
using AiTravelAgent.Configuration;
using AiTravelAgent.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AiTravelAgent.Services;

public class AmapService
{
    private static AmapService? _instance;
    private static readonly object _lock = new();

    private readonly HttpClient _httpClient;
    private readonly AppSettings _settings;

    private AmapService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _settings = ConfigurationLoader.LoadSettings();
    }

    public static AmapService GetInstance()
    {
        if (_instance == null)
        {
            lock (_lock)
            {
                _instance ??= new AmapService();
            }
        }
        return _instance;
    }

    public async Task<List<POIInfo>> SearchPOIAsync(string keywords, string city, bool citylimit = true)
    {
        var result = new List<POIInfo>();

        if (string.IsNullOrEmpty(_settings.AmapWebKey))
        {
            Console.WriteLine("Warning: Amap Web Key not configured");
            return result;
        }

        try
        {
            var url = $"https://restapi.amap.com/v3/place/text?keywords={HttpUtility.UrlEncode(keywords)}&city={HttpUtility.UrlEncode(city)}&offset=20&key={_settings.AmapWebKey}";

            if (citylimit)
            {
                url += "&citylimit=true";
            }

            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);

            if (json["status"]?.ToString() == "1" && json["pois"] != null)
            {
                foreach (var poi in json["pois"]!)
                {
                    var locationStr = poi["location"]?.ToString();
                    var parts = locationStr?.Split(',');

                    result.Add(new POIInfo
                    {
                        Id = poi["id"]?.ToString() ?? "",
                        Name = poi["name"]?.ToString() ?? "",
                        Type = poi["type"]?.ToString() ?? "",
                        Address = poi["address"]?.ToString() ?? "",
                        Location = parts?.Length == 2
                            ? new Location
                            {
                                Longitude = double.Parse(parts[0]),
                                Latitude = double.Parse(parts[1])
                            }
                            : new Location(),
                        Tel = poi["tel"]?.ToString()
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Amap POI Search Error: {ex.Message}");
        }

        return result;
    }

    public async Task<Location?> GeocodeAsync(string address, string city)
    {
        if (string.IsNullOrEmpty(_settings.AmapWebKey))
        {
            Console.WriteLine("Warning: Amap Web Key not configured");
            return null;
        }

        try
        {
            var url = $"https://restapi.amap.com/v3/geocode/geo?address={HttpUtility.UrlEncode(address)}&city={HttpUtility.UrlEncode(city)}&key={_settings.AmapWebKey}";

            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);

            if (json["status"]?.ToString() == "1" && json["geocodes"] != null && json["geocodes"].HasValues)
            {
                var locationStr = json["geocodes"]?[0]?["location"]?.ToString();
                var parts = locationStr?.Split(',');

                if (parts?.Length == 2)
                {
                    return new Location
                    {
                        Longitude = double.Parse(parts[0]),
                        Latitude = double.Parse(parts[1])
                    };
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Amap Geocode Error: {ex.Message}");
        }

        return null;
    }

    public async Task<List<WeatherInfo>> GetWeatherAsync(string city)
    {
        var result = new List<WeatherInfo>();

        if (string.IsNullOrEmpty(_settings.AmapWebKey))
        {
            Console.WriteLine("Warning: Amap Web Key not configured");
            return result;
        }

        try
        {
            var cleanCity = city.Split('-').Last().Trim();

            var url = $"https://restapi.amap.com/v3/weather/weatherInfo?key={_settings.AmapWebKey}&city={HttpUtility.UrlEncode(cleanCity)}&extensions=all&output=JSON";

            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);

            var forecasts = json["forecasts"] as JArray;
            if (forecasts != null && forecasts.Count > 0)
            {
                var casts = forecasts[0]["casts"] as JArray;
                if (casts != null)
                {
                    foreach (var c in casts)
                    {
                        result.Add(new WeatherInfo
                        {
                            Date = c["date"]?.ToString() ?? "",
                            DayWeather = c["dayweather"]?.ToString() ?? "",
                            NightWeather = c["nightweather"]?.ToString() ?? "",
                            DayTemp = ParseTemperature(c["daytemp"]?.ToString()),
                            NightTemp = ParseTemperature(c["nighttemp"]?.ToString()),
                            WindDirection = c["daywind"]?.ToString() ?? "",
                            WindPower = c["daypower"]?.ToString() ?? ""
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Amap Weather Error: {ex.Message}");
        }

        return result;
    }

    private static object ParseTemperature(string? temp)
    {
        if (string.IsNullOrEmpty(temp))
            return 0;

        var cleaned = temp.Replace("°C", "").Replace("℃", "").Replace("°", "").Trim();

        if (int.TryParse(cleaned, out var result))
            return result;

        return 0;
    }

    public async Task<RouteInfo?> PlanRouteAsync(string originAddress, string destinationAddress, string routeType = "walking")
    {
        if (string.IsNullOrEmpty(_settings.AmapWebKey))
        {
            Console.WriteLine("Warning: Amap Web Key not configured");
            return null;
        }

        try
        {
            var url = $"https://restapi.amap.com/v3/direction/{routeType}?origin={HttpUtility.UrlEncode(originAddress)}&destination={HttpUtility.UrlEncode(destinationAddress)}&key={_settings.AmapWebKey}";

            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);

            if (json["status"]?.ToString() == "1" && json["route"] != null)
            {
                var paths = json["route"]["paths"] as JArray;
                if (paths != null && paths.HasValues)
                {
                    var path = paths[0];
                    return new RouteInfo
                    {
                        Distance = double.Parse(path["distance"]?.ToString() ?? "0"),
                        Duration = int.Parse(path["time"]?.ToString() ?? "0"),
                        RouteType = routeType,
                        Description = $"距离 {path["distance"]} 米，预计需要 {path["time"]} 秒"
                    };
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Amap Route Error: {ex.Message}");
        }

        return null;
    }
}
