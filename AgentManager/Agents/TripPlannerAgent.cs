using System.Text.Json;
using System.Text.RegularExpressions;
using AiTravelAgent.Configuration;
using AiTravelAgent.Models;
using AiTravelAgent.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AiTravelAgent.Agents;

public class TripPlannerAgent
{
    private static TripPlannerAgent? _instance;
    private static readonly object _lock = new();

    private readonly string _plannerSystemPrompt;

    private TripPlannerAgent()
    {
        _plannerSystemPrompt = @"你是行程规划专家。你的任务是根据景点信息和天气信息,生成详细的旅行计划。

请严格按照以下JSON格式返回旅行计划:
```json
{
  ""city"": ""城市名称"",
  ""start_date"": ""YYYY-MM-DD"",
  ""end_date"": ""YYYY-MM-DD"",
  ""days"": [
    {
      ""date"": ""YYYY-MM-DD"",
      ""day_index"": 0,
      ""description"": ""第1天行程概述"",
      ""transportation"": ""交通方式"",
      ""accommodation"": ""住宿类型"",
      ""hotel"": {
        ""name"": ""酒店名称"",
        ""address"": ""酒店地址"",
        ""location"": {""longitude"": 116.397128, ""latitude"": 39.916527},
        ""price_range"": ""300-500元"",
        ""rating"": ""4.5"",
        ""distance"": ""距离景点2公里"",
        ""type"": ""经济型酒店"",
        ""estimated_cost"": 400
      },
      ""attractions"": [
        {
          ""name"": ""景点名称"",
          ""address"": ""详细地址"",
          ""location"": {""longitude"": 116.397128, ""latitude"": 39.916527},
          ""visit_duration"": 120,
          ""description"": ""景点详细描述"",
          ""category"": ""景点类别"",
          ""ticket_price"": 60,
          ""reservation_required"": false,
          ""reservation_tips"": """"
        }
      ],
      ""meals"": [
        {""type"": ""breakfast"", ""name"": ""早餐推荐"", ""description"": ""早餐描述"", ""estimated_cost"": 30},
        {""type"": ""lunch"", ""name"": ""午餐推荐"", ""description"": ""午餐描述"", ""estimated_cost"": 50},
        {""type"": ""dinner"", ""name"": ""晚餐推荐"", ""description"": ""晚餐描述"", ""estimated_cost"": 80}
      ]
    }
  ],
  ""weather_info"": [
    {
      ""date"": ""YYYY-MM-DD"",
      ""day_weather"": ""晴"",
      ""night_weather"": ""多云"",
      ""day_temp"": 25,
      ""night_temp"": 15,
      ""wind_direction"": ""南风"",
      ""wind_power"": ""1-3级""
    }
  ],
  ""overall_suggestions"": ""总体建议"",
  ""budget"": {
    ""total_attractions"": 180,
    ""total_hotels"": 1200,
    ""total_meals"": 480,
    ""total_transportation"": 200,
    ""total"": 2060
  }
}
```

**重要提示:**
1. weather_info数组必须包含每一天的天气信息
2. 温度必须是纯数字(不要带C等单位)
3. 每天安排2-3个景点
4. 考虑景点之间的距离和游览时间
5. 每天必须包含早中晚三餐
6. 提供实用的旅行建议
7. **必须包含预算信息**";
    }

    public static TripPlannerAgent GetInstance()
    {
        if (_instance == null)
        {
            lock (_lock)
            {
                _instance ??= new TripPlannerAgent();
            }
        }
        return _instance;
    }

    public async Task<TripPlan> PlanTripAsync(TripRequest request, Action<string, string, int>? progressCallback = null)
    {
        Console.WriteLine($"\n{new string('=', 60)}");
        Console.WriteLine("开始规划旅行...");
        Console.WriteLine($"目的地: {request.City}");
        Console.WriteLine($"日期: {request.StartDate} 至 {request.EndDate}");
        Console.WriteLine($"天数: {request.TravelDays}天");
        Console.WriteLine($"偏好: {string.Join(", ", request.Preferences)}");
        Console.WriteLine($"{new string('=', 60)}\n");

        progressCallback?.Invoke("initializing", "正在初始化...", 10);

        var attractionsInfo = await SearchAttractionsAsync(request);
        progressCallback?.Invoke("attraction_search", "景点搜索完成", 30);

        var weatherInfo = await SearchWeatherAsync(request);
        progressCallback?.Invoke("weather_search", "天气查询完成", 50);

        var hotelInfo = await SearchHotelsAsync(request);
        progressCallback?.Invoke("hotel_search", "酒店搜索完成", 70);

        progressCallback?.Invoke("planning", "正在生成旅行计划...", 85);

        var tripPlan = await GenerateTripPlanAsync(request, attractionsInfo, weatherInfo, hotelInfo);

        progressCallback?.Invoke("completed", "旅行计划生成完成", 100);

        Console.WriteLine($"\n{new string('=', 60)}");
        Console.WriteLine("旅行计划生成完成!");
        Console.WriteLine($"{new string('=', 60)}\n");

        return tripPlan;
    }

    private async Task<string> SearchAttractionsAsync(TripRequest request)
    {
        Console.WriteLine("  [1/3] 正在搜索景点...");

        var keywords = request.Preferences?.FirstOrDefault() ?? "景点";
        var city = request.City;

        try
        {
            var amap = AmapService.GetInstance();
            var pois = await amap.SearchPOIAsync(keywords, city);

            if (pois.Count == 0)
            {
                return $"在{city}未找到相关景点信息。";
            }

            var result = $"小红书热门精选游记的提取结果，附带确切坐标：\n";
            foreach (var poi in pois.Take(4))
            {
                result += JsonConvert.SerializeObject(new
                {
                    name = poi.Name,
                    name_zh = poi.Name,
                    name_en = poi.Name,
                    reason = $"位于{city}的热门景点",
                    duration = 120,
                    reservation_required = false,
                    reservation_tips = "",
                    location = new { longitude = poi.Location.Longitude, latitude = poi.Location.Latitude }
                }) + "\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"景点搜索异常: {ex.Message}");
            return $"景点搜索失败: {ex.Message}";
        }
    }

    private async Task<string> SearchWeatherAsync(TripRequest request)
    {
        Console.WriteLine("  [2/3] 正在查询天气...");

        try
        {
            var amap = AmapService.GetInstance();
            var weatherList = await amap.GetWeatherAsync(request.City);

            if (weatherList.Count == 0)
            {
                return $"无法获取{request.City}的天气信息。";
            }

            var result = "";
            foreach (var w in weatherList.Take(request.TravelDays))
            {
                result += $"{w.Date}: 白天{w.DayWeather} {w.DayTemp}C, 夜间{w.NightWeather} {w.NightTemp}C, {w.WindDirection}风 {w.WindPower}\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"天气查询异常: {ex.Message}");
            return $"天气查询失败: {ex.Message}";
        }
    }

    private async Task<string> SearchHotelsAsync(TripRequest request)
    {
        Console.WriteLine("  [3/3] 正在搜索酒店...");

        try
        {
            var amap = AmapService.GetInstance();
            var hotels = await amap.SearchPOIAsync("酒店", request.City);

            if (hotels.Count == 0)
            {
                return $"在{request.City}未找到酒店信息。";
            }

            var result = "";
            foreach (var h in hotels.Take(3))
            {
                result += $"酒店: {h.Name}, 地址: {h.Address}, 类型: {request.Accommodation}\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"酒店搜索异常: {ex.Message}");
            return $"酒店搜索失败: {ex.Message}";
        }
    }

    private async Task<TripPlan> GenerateTripPlanAsync(TripRequest request, string attractions, string weather, string hotels)
    {
        var settings = ConfigurationLoader.LoadSettings();
        
        if (string.IsNullOrEmpty(settings.OpenAiApiKey) || settings.OpenAiBaseUrl.Contains("volces.com"))
        {
            return await GenerateMockTripPlanAsync(request);
        }

        var lang = (request.Language ?? "zh").Trim().ToLower();
        var langSuffix = "";
        if (lang != "zh")
        {
            langSuffix = $"\n\n**语言要求 (Language Requirement):**\n请用 {(lang == "en" ? "English" : lang)} 语言输出所有文字内容。";
        }

        var query = $@"请根据以下信息生成{request.City}的{request.TravelDays}天旅行计划:

**基本信息:**
- 城市: {request.City}
- 日期: {request.StartDate} 至 {request.EndDate}
- 天数: {request.TravelDays}天
- 交通方式: {request.Transportation}
- 住宿: {request.Accommodation}
- 偏好: {string.Join(", ", request.Preferences ?? new List<string>())}

**景点信息:**
{attractions}

**天气信息:**
{weather}

**酒店信息:**
{hotels}

**要求:**
1. 每天安排2-3个景点
2. 每天必须包含早中晚三餐
3. 每天推荐一个具体的酒店(从酒店信息中选择)
4. 考虑景点之间的距离和交通方式
5. 返回完整的JSON格式数据
6. 景点的经纬度坐标要真实准确
7. 如果天气或酒店信息不足，请基于保守、通用的旅行建议补齐{langSuffix}";

        if (!string.IsNullOrEmpty(request.FreeTextInput))
        {
            query += $"\n**额外要求:** {request.FreeTextInput}";
        }

        var llm = LLMService.GetInstance();
        var response = await llm.ChatCompletionAsync(_plannerSystemPrompt, query, maxTokens: 4000, temperature: 0.2);

        Console.WriteLine($"行程规划结果: {response.Substring(0, Math.Min(300, response.Length))}...\n");

        return ParseTripPlanResponse(response, request);
    }

    private TripPlan ParseTripPlanResponse(string response, TripRequest request)
    {
        try
        {
            var jsonStr = ExtractJson(response);

            jsonStr = SanitizeJson(jsonStr);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var tripPlan = System.Text.Json.JsonSerializer.Deserialize<TripPlan>(jsonStr, options);

            if (tripPlan != null)
            {
                return tripPlan;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"JSON解析异常: {ex.Message}");
        }

        return CreateFallbackPlan(request);
    }

    private string ExtractJson(string response)
    {
        if (response.Contains("```json"))
        {
            var start = response.IndexOf("```json") + 7;
            var end = response.IndexOf("```", start);
            if (end > start)
                return response.Substring(start, end - start).Trim();
        }

        if (response.Contains("```"))
        {
            var start = response.IndexOf("```") + 3;
            var end = response.IndexOf("```", start);
            if (end > start)
                return response.Substring(start, end - start).Trim();
        }

        if (response.Contains("{"))
        {
            var start = response.IndexOf("{");
            var end = response.LastIndexOf("}");
            if (end > start)
                return response.Substring(start, end - start + 1);
        }

        return response;
    }

    private string SanitizeJson(string jsonStr)
    {
        jsonStr = jsonStr.Trim();

        jsonStr = Regex.Replace(jsonStr, @"^```(?:json)?\s*", "", RegexOptions.IgnoreCase);
        jsonStr = Regex.Replace(jsonStr, @"```\s*$", "", RegexOptions.IgnoreCase);

        jsonStr = Regex.Replace(jsonStr, @"//[^\n]*", "");
        jsonStr = Regex.Replace(jsonStr, @"/\*.*?\*/", "", RegexOptions.Singleline);

        jsonStr = jsonStr.Replace("\u201c", "'").Replace("\u201d", "'");
        jsonStr = jsonStr.Replace("\u2018", "'").Replace("\u2019", "'");

        jsonStr = Regex.Replace(jsonStr, @",\s*([\]\}])", "$1");

        jsonStr = Regex.Replace(jsonStr, @":\s*(\d+(?:\s*[+\-*/]\s*\d+)+)", match =>
        {
            var expr = match.Groups[1].Value.Trim();
            if (expr.Contains("="))
            {
                return match.Value.Replace(expr, expr.Split('=').Last().Trim());
            }
            try
            {
                var result = EvaluateArithmetic(expr);
                return match.Value.Replace(expr, result.ToString());
            }
            catch
            {
                return match.Value;
            }
        });

        return jsonStr;
    }

    private double EvaluateArithmetic(string expr)
    {
        expr = Regex.Replace(expr, @"\s+", "");
        var tokens = Regex.Split(expr, @"([+\-*/])").Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();

        if (tokens.Length == 0)
            return 0;

        var result = double.Parse(tokens[0]);
        for (int i = 1; i < tokens.Length - 1; i += 2)
        {
            var op = tokens[i];
            var val = double.Parse(tokens[i + 1]);
            result = op switch
            {
                "+" => result + val,
                "-" => result - val,
                "*" => result * val,
                "/" => val != 0 ? result / val : result,
                _ => result
            };
        }

        return result;
    }

    private async Task<TripPlan> GenerateMockTripPlanAsync(TripRequest request)
    {
        Console.WriteLine("使用模拟模式生成旅行计划...");

        var cityInfo = GetCityInfo(request.City);
        var days = new List<DayPlan>();
        var startDate = DateTime.Parse(request.StartDate);
        var weatherList = new List<WeatherInfo>();

        for (int i = 0; i < request.TravelDays; i++)
        {
            var currentDate = startDate.AddDays(i);

            weatherList.Add(new WeatherInfo
            {
                Date = currentDate.ToString("yyyy-MM-dd"),
                DayWeather = "晴",
                NightWeather = "多云",
                DayTemp = 25 + i,
                NightTemp = 15 + i,
                WindDirection = "南风",
                WindPower = "1-3级"
            });

            var dayAttractions = new List<Attraction>();
            for (int j = 0; j < 3; j++)
            {
                var idx = (i * 3 + j) % ((Array)cityInfo.Attractions).Length;
                var attr = cityInfo.Attractions[idx];
                dayAttractions.Add(new Attraction
                {
                    Name = attr.Name,
                    Address = attr.Address,
                    Location = new Location { Longitude = attr.Longitude, Latitude = attr.Latitude },
                    VisitDuration = 120,
                    Description = attr.Description,
                    Category = attr.Category,
                    TicketPrice = attr.TicketPrice,
                    ReservationRequired = false,
                    ImageUrl = attr.ImageUrl
                });
            }

            days.Add(new DayPlan
            {
                Date = currentDate.ToString("yyyy-MM-dd"),
                DayIndex = i + 1,
                Description = $"第{i + 1}天：{cityInfo.DayDescriptions[i % ((Array)cityInfo.DayDescriptions).Length]}",
                Transportation = request.Transportation,
                Accommodation = request.Accommodation,
                Hotel = new Hotel
                {
                    Name = cityInfo.HotelName,
                    Address = cityInfo.HotelAddress,
                    Location = new Location { Longitude = cityInfo.HotelLng, Latitude = cityInfo.HotelLat },
                    PriceRange = cityInfo.HotelPriceRange,
                    Rating = "4.5",
                    Distance = "距离景点中心约2公里",
                    Type = request.Accommodation,
                    EstimatedCost = cityInfo.HotelCost
                },
                Attractions = dayAttractions,
                Meals = new List<Meal>
                {
                    new Meal { Type = "breakfast", Name = cityInfo.Breakfast, Description = "当地特色早餐", EstimatedCost = 30 },
                    new Meal { Type = "lunch", Name = cityInfo.Lunch, Description = "当地特色午餐", EstimatedCost = 60 },
                    new Meal { Type = "dinner", Name = cityInfo.Dinner, Description = "当地特色晚餐", EstimatedCost = 80 }
                }
            });
        }

        int totalAttractions = days.Sum(d => d.Attractions.Sum(a => a.TicketPrice));
        int totalHotels = days.Count * cityInfo.HotelCost;
        int totalMeals = days.Count * 170;

        return new TripPlan
        {
            City = request.City,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Days = days,
            WeatherInfo = weatherList,
            OverallSuggestions = $"这是为您规划的{request.City}{request.TravelDays}日游行程。{cityInfo.Suggestions}\n\n出行提示：\n1. 建议提前预约热门景点门票\n2. {request.Transportation}出行请提前规划路线\n3. 注意天气变化，做好防晒或防雨准备",
            Budget = new Budget
            {
                TotalAttractions = totalAttractions,
                TotalHotels = totalHotels,
                TotalMeals = totalMeals,
                TotalTransportation = request.TravelDays * 50,
                Total = totalAttractions + totalHotels + totalMeals + request.TravelDays * 50
            }
        };
    }

    private dynamic GetCityInfo(string city)
    {
        if (city.Contains("成都"))
        {
            return new
            {
                Attractions = new[]
                {
                    new { Name = "宽窄巷子", Address = "四川省成都市青羊区金河宾馆北侧", Longitude = 104.067923, Latitude = 30.66359, Description = "清代古街道，体验老成都生活", Category = "历史文化", TicketPrice = 0, ImageUrl = "data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNDAwIiBoZWlnaHQ9IjMwMCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48cmVjdCB3aWR0aD0iMTAwJSIgaGVpZ2h0PSIxMDAlIiBmaWxsPSIjMGEyNjJmIi8+PHRleHQgeD0iNTAlIiB5PSI1MCUiIGZvbnQtc2l6ZT0iMjQiIGZvbnQtZmFtaWx5PSJBcmlhbCIgc3Ryb2tlPSJyZ2JhKDI1NSwyNTUsMjU1LDAuNCkiIHN0cm9rZS13aWR0aD0iMSIgdGV4dC1hbmNob3I9Im1pZGRsZSI+5aWH6I2J6LSFPC90ZXh0Pjwvc3ZnPg==" },
                    new { Name = "锦里古街", Address = "四川省成都市武侯区武侯祠大街231号", Longitude = 104.05429, Latitude = 30.64633, Description = "古蜀文化商业街，美食聚集地", Category = "历史文化", TicketPrice = 0, ImageUrl = "data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNDAwIiBoZWlnaHQ9IjMwMCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48cmVjdCB3aWR0aD0iMTAwJSIgaGVpZ2h0PSIxMDAlIiBmaWxsPSIjMWNiOTNjIi8+PHRleHQgeD0iNTAlIiB5PSI1MCUiIGZvbnQtc2l6ZT0iMjQiIGZvbnQtZmFtaWx5PSJBcmlhbCIgc3Ryb2tlPSJyZ2JhKDI1NSwyNTUsMjU1LDAuNCkiIHN0cm9rZS13aWR0aD0iMSIgdGV4dC1hbmNob3I9Im1pZGRsZSI+5bCP5LiK5p2h5ryUPC90ZXh0Pjwvc3ZnPg==" },
                    new { Name = "武侯祠", Address = "四川省成都市武侯区武侯祠大街231号", Longitude = 104.05498, Latitude = 30.64677, Description = "纪念诸葛亮的祠宇", Category = "历史文化", TicketPrice = 50, ImageUrl = "data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNDAwIiBoZWlnaHQ9IjMwMCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48cmVjdCB3aWR0aD0iMTAwJSIgaGVpZ2h0PSIxMDAlIiBmaWxsPSIjMjJjYmNmIi8+PHRleHQgeD0iNTAlIiB5PSI1MCUiIGZvbnQtc2l6ZT0iMjQiIGZvbnQtZmFtaWx5PSJBcmlhbCIgc3Ryb2tlPSJyZ2JhKDI1NSwyNTUsMjU1LDAuNCkiIHN0cm9rZS13aWR0aD0iMSIgdGV4dC1hbmNob3I9Im1pZGRsZSI+54ix5b+D5aWHPC90ZXh0Pjwvc3ZnPg==" },
                    new { Name = "杜甫草堂", Address = "四川省成都市青羊区青华路37号", Longitude = 104.03954, Latitude = 30.66028, Description = "唐代诗人杜甫的故居", Category = "历史文化", TicketPrice = 50, ImageUrl = "data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNDAwIiBoZWlnaHQ9IjMwMCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48cmVjdCB3aWR0aD0iMTAwJSIgaGVpZ2h0PSIxMDAlIiBmaWxsPSIjMzZjNDI0Ii8+PHRleHQgeD0iNTAlIiB5PSI1MCUiIGZvbnQtc2l6ZT0iMjQiIGZvbnQtZmFtaWx5PSJBcmlhbCIgc3Ryb2tlPSJyZ2JhKDI1NSwyNTUsMjU1LDAuNCkiIHN0cm9rZS13aWR0aD0iMSIgdGV4dC1hbmNob3I9Im1pZGRsZSI+5bKt5LiJ6JCl6LW3PC90ZXh0Pjwvc3ZnPg==" },
                    new { Name = "大熊猫基地", Address = "四川省成都市成华区外北熊猫大道1375号", Longitude = 104.1799, Latitude = 30.74644, Description = "世界著名的大熊猫保护研究基地", Category = "自然景观", TicketPrice = 55, ImageUrl = "data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNDAwIiBoZWlnaHQ9IjMwMCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48cmVjdCB3aWR0aD0iMTAwJSIgaGVpZ2h0PSIxMDAlIiBmaWxsPSIjMWM4OWEwIi8+PHRleHQgeD0iNTAlIiB5PSI1MCUiIGZvbnQtc2l6ZT0iMjQiIGZvbnQtZmFtaWx5PSJBcmlhbCIgc3Ryb2tlPSJyZ2JhKDI1NSwyNTUsMjU1LDAuNCkiIHN0cm9rZS13aWR0aD0iMSIgdGV4dC1hbmNob3I9Im1pZGRsZSI+5aSn5bee5aWH5Z+65Z2hPC90ZXh0Pjwvc3ZnPg==" },
                    new { Name = "都江堰", Address = "四川省成都市都江堰市公园路都江堰景区", Longitude = 103.61459, Latitude = 30.99758, Description = "世界文化遗产，古代水利工程", Category = "历史文化", TicketPrice = 80, ImageUrl = "data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNDAwIiBoZWlnaHQ9IjMwMCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48cmVjdCB3aWR0aD0iMTAwJSIgaGVpZ2h0PSIxMDAlIiBmaWxsPSIjNTdjOGMwIi8+PHRleHQgeD0iNTAlIiB5PSI1MCUiIGZvbnQtc2l6ZT0iMjQiIGZvbnQtZmFtaWx5PSJBcmlhbCIgc3Ryb2tlPSJyZ2JhKDI1NSwyNTUsMjU1LDAuNCkiIHN0cm9rZS13aWR0aD0iMSIgdGV4dC1hbmNob3I9Im1pZGRsZSI+5LmL5a6J5ryrPC90ZXh0Pjwvc3ZnPg==" }
                },
                DayDescriptions = new[] { "市区文化之旅", "熊猫与自然之旅", "都江堰一日游" },
                HotelName = "成都宽窄巷子美居酒店",
                HotelAddress = "成都市青羊区宽窄巷子附近",
                HotelLng = 104.067,
                HotelLat = 30.664,
                HotelPriceRange = "300-500元",
                HotelCost = 400,
                Breakfast = "担担面+豆浆",
                Lunch = "川菜套餐",
                Dinner = "火锅",
                Suggestions = "成都美食众多，建议品尝正宗川菜和火锅。"
            };
        }
        else if (city.Contains("重庆"))
        {
            return new
            {
                Attractions = new[]
                {
                    new { Name = "洪崖洞", Address = "重庆市渝中区嘉陵江滨江路88号", Longitude = 106.58721, Latitude = 29.49465, Description = "巴渝传统吊脚楼建筑群", Category = "历史文化", TicketPrice = 0, ImageUrl = "" },
                    new { Name = "解放碑", Address = "重庆市渝中区民族路177号", Longitude = 106.58604, Latitude = 29.43163, Description = "重庆地标性建筑", Category = "历史文化", TicketPrice = 0, ImageUrl = "" },
                    new { Name = "长江索道", Address = "重庆市渝中区新华路153号", Longitude = 106.59636, Latitude = 29.43357, Description = "横跨长江的空中交通", Category = "自然景观", TicketPrice = 30, ImageUrl = "" },
                    new { Name = "磁器口古镇", Address = "重庆市沙坪坝区磁器口镇", Longitude = 106.45633, Latitude = 29.49848, Description = "千年古镇，民俗文化聚集地", Category = "历史文化", TicketPrice = 0, ImageUrl = "" },
                    new { Name = "武隆天生三桥", Address = "重庆市武隆区仙女山镇", Longitude = 107.77333, Latitude = 29.28889, Description = "世界自然遗产，喀斯特地貌", Category = "自然景观", TicketPrice = 125, ImageUrl = "" },
                    new { Name = "李子坝轻轨站", Address = "重庆市渝中区李子坝正街", Longitude = 106.57327, Latitude = 29.46103, Description = "穿楼而过的网红轻轨", Category = "现代景观", TicketPrice = 0, ImageUrl = "" }
                },
                DayDescriptions = new[] { "渝中半岛之旅", "古镇与美食之旅", "武隆自然之旅" },
                HotelName = "重庆解放碑威斯汀酒店",
                HotelAddress = "重庆市渝中区解放碑附近",
                HotelLng = 106.586,
                HotelLat = 29.432,
                HotelPriceRange = "400-600元",
                HotelCost = 500,
                Breakfast = "小面+油茶",
                Lunch = "江湖菜",
                Dinner = "重庆火锅",
                Suggestions = "重庆地势起伏大，建议穿舒适的鞋子。"
            };
        }
        else
        {
            return new
            {
                Attractions = new[]
                {
                    new { Name = "市中心广场", Address = $"{city}市中心", Longitude = 116.40, Latitude = 39.90, Description = $"{city}城市中心地标", Category = "现代景观", TicketPrice = 0, ImageUrl = "" },
                    new { Name = "历史博物馆", Address = $"{city}历史文化区", Longitude = 116.41, Latitude = 39.91, Description = $"{city}历史文化展示", Category = "历史文化", TicketPrice = 30, ImageUrl = "" },
                    new { Name = "湿地公园", Address = $"{city}郊区", Longitude = 116.39, Latitude = 39.89, Description = "城市绿肺，自然风光", Category = "自然景观", TicketPrice = 20, ImageUrl = "" },
                    new { Name = "美食街", Address = $"{city}商业街", Longitude = 116.42, Latitude = 39.92, Description = $"{city}特色美食聚集地", Category = "美食", TicketPrice = 0, ImageUrl = "" },
                    new { Name = "文化艺术中心", Address = $"{city}新区", Longitude = 116.43, Latitude = 39.88, Description = "现代文化艺术场馆", Category = "艺术", TicketPrice = 50, ImageUrl = "" },
                    new { Name = "古城墙", Address = $"{city}老城区", Longitude = 116.38, Latitude = 39.93, Description = "历史古城墙遗址", Category = "历史文化", TicketPrice = 40, ImageUrl = "" }
                },
                DayDescriptions = new[] { "城市地标之旅", "文化探索之旅", "自然休闲之旅" },
                HotelName = $"{city}市中心酒店",
                HotelAddress = $"{city}市中心商业区",
                HotelLng = 116.40,
                HotelLat = 39.90,
                HotelPriceRange = "300-500元",
                HotelCost = 400,
                Breakfast = "中式早餐",
                Lunch = $"{city}特色菜",
                Dinner = $"当地美食",
                Suggestions = $"欢迎来到{city}旅游，祝您旅途愉快。"
            };
        }
    }

    private TripPlan CreateFallbackPlan(TripRequest request)
    {
        var days = new List<DayPlan>();
        var startDate = DateTime.Parse(request.StartDate);

        for (int i = 0; i < request.TravelDays; i++)
        {
            var currentDate = startDate.AddDays(i);

            days.Add(new DayPlan
            {
                Date = currentDate.ToString("yyyy-MM-dd"),
                DayIndex = i,
                Description = $"第{i + 1}天行程",
                Transportation = request.Transportation,
                Accommodation = request.Accommodation,
                Attractions = new List<Attraction>
                {
                    new Attraction
                    {
                        Name = $"{request.City}景点1",
                        Address = $"{request.City}市",
                        Location = new Location { Longitude = 116.4 + i * 0.01, Latitude = 39.9 + i * 0.01 },
                        VisitDuration = 120,
                        Description = $"这是{request.City}的著名景点",
                        Category = "景点"
                    },
                    new Attraction
                    {
                        Name = $"{request.City}景点2",
                        Address = $"{request.City}市",
                        Location = new Location { Longitude = 116.41 + i * 0.01, Latitude = 39.91 + i * 0.01 },
                        VisitDuration = 120,
                        Description = $"这是{request.City}的著名景点",
                        Category = "景点"
                    }
                },
                Meals = new List<Meal>
                {
                    new Meal { Type = "breakfast", Name = $"第{i + 1}天早餐", Description = "当地特色早餐", EstimatedCost = 30 },
                    new Meal { Type = "lunch", Name = $"第{i + 1}天午餐", Description = "午餐推荐", EstimatedCost = 50 },
                    new Meal { Type = "dinner", Name = $"第{i + 1}天晚餐", Description = "晚餐推荐", EstimatedCost = 80 }
                }
            });
        }

        return new TripPlan
        {
            City = request.City,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Days = days,
            WeatherInfo = new List<WeatherInfo>(),
            OverallSuggestions = $"这是为您规划的{request.City}{request.TravelDays}日游行程,建议提前查看各景点的开放时间。"
        };
    }
}
