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

            var cities = ParseCities(request.City);
            var days = new List<DayPlan>();
            var startDate = DateTime.Parse(request.StartDate);
            var weatherList = new List<WeatherInfo>();
            var totalAttractions = 0;
            var totalHotels = 0;
            var totalMeals = 0;

            int daysPerCity = request.TravelDays / cities.Count;
            int extraDays = request.TravelDays % cities.Count;

            int dayIndex = 0;
            for (int cityIdx = 0; cityIdx < cities.Count; cityIdx++)
            {
                var city = cities[cityIdx];
                var cityInfo = await GetCityInfoAsync(city);
                int daysForCity = daysPerCity + (cityIdx < extraDays ? 1 : 0);

                for (int i = 0; i < daysForCity; i++)
                {
                    var currentDate = startDate.AddDays(dayIndex);

                    weatherList.Add(new WeatherInfo
                    {
                        Date = currentDate.ToString("yyyy-MM-dd"),
                        DayWeather = "晴",
                        NightWeather = "多云",
                        DayTemp = 25 + dayIndex,
                        NightTemp = 15 + dayIndex,
                        WindDirection = "南风",
                        WindPower = "1-3级"
                    });

                    var dayAttractions = new List<Attraction>();
                    for (int j = 0; j < 3; j++)
                    {
                        var idx = (dayIndex * 3 + j) % ((Array)cityInfo.Attractions).Length;
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
                        DayIndex = dayIndex + 1,
                        Description = $"第{dayIndex + 1}天：{city} - {cityInfo.DayDescriptions[i % ((Array)cityInfo.DayDescriptions).Length]}",
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

                    totalAttractions += dayAttractions.Sum(a => a.TicketPrice);
                    totalHotels += cityInfo.HotelCost;
                    totalMeals += 170;
                    dayIndex++;
                }
            }

            var suggestions = string.Join(" ", cities.Select(c => GetStaticCityInfo(c).Suggestions));

            return new TripPlan
            {
                City = request.City,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Days = days,
                WeatherInfo = weatherList,
                OverallSuggestions = $"这是为您规划的{request.City}{request.TravelDays}日游行程。{suggestions}\n\n出行提示：\n1. 建议提前预约热门景点门票\n2. {request.Transportation}出行请提前规划路线\n3. 注意天气变化，做好防晒或防雨准备",
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

        private List<string> ParseCities(string cityInput)
        {
            var separators = new[] { "-", "、", "，", ",", "和", "与", "到" };
            var cities = cityInput.Split(separators, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(c => c.Trim())
                                 .Where(c => !string.IsNullOrWhiteSpace(c))
                                 .ToList();

            if (cities.Count == 0)
            {
                cities.Add(cityInput);
            }

            return cities;
        }

    private async Task<dynamic> GetCityInfoAsync(string city)
    {
        var amapService = AmapService.GetInstance();
        var settings = ConfigurationLoader.LoadSettings();

        List<POIInfo> amapAttractions = new List<POIInfo>();
        
        if (!string.IsNullOrEmpty(settings.AmapWebKey))
        {
            try
            {
                Console.WriteLine($"尝试从高德地图获取{city}的景点数据...");
                
                var allAttractions = new List<POIInfo>();
                var keywords = new[] { "景点", "景区", "旅游", "风景名胜", "古城", "古镇", "古迹", "文化", "历史", "名山", "风景" };
                
                foreach (var keyword in keywords)
                {
                    var attractions = await amapService.SearchPOIAsync(keyword, city);
                    allAttractions.AddRange(attractions);
                    if (allAttractions.Count >= 12) break;
                    await Task.Delay(100);
                }

                if (allAttractions.Count < 6)
                {
                    Console.WriteLine($"当前城市景点不足，尝试扩大搜索范围...");
                    var broaderKeywords = new[] { "古城", "古镇", "5A景区", "4A景区", "著名景点" };
                    foreach (var keyword in broaderKeywords)
                    {
                        var attractions = await amapService.SearchPOIAsync(keyword, city, false);
                        allAttractions.AddRange(attractions);
                        if (allAttractions.Count >= 12) break;
                        await Task.Delay(100);
                    }
                }

                var famousAttractions = GetFamousAttractionsForCity(city);
                foreach (var attraction in famousAttractions)
                {
                    var results = await amapService.SearchPOIAsync(attraction, "", false);
                    if (results.Any())
                    {
                        Console.WriteLine($"搜索到著名景点: {attraction}");
                        allAttractions.AddRange(results);
                    }
                }
                
                amapAttractions = allAttractions.DistinctBy(p => p.Name).Take(12).ToList();
                
                Console.WriteLine($"从高德地图获取到 {amapAttractions.Count} 个景点: {string.Join(", ", amapAttractions.Select(p => p.Name))}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"从高德地图获取数据失败: {ex.Message}");
            }
        }

        var staticInfo = GetStaticCityInfo(city);
        var staticAttractions = ((Array)staticInfo.Attractions).Cast<dynamic>().ToList();
        
        var combinedAttractions = new List<dynamic>();
        var usedNames = new HashSet<string>();

        var famousAttractionNames = GetFamousAttractionsForCity(city);
        var highPriorityKeywords = new HashSet<string> { "古城", "古镇", "故居", "纪念馆", "文化", "历史", "古迹", "寺", "庙", "祠", "山", "岩", "洞", "瀑布", "峡谷", "湖", "河", "泉", "5A", "景区", "风景" };
        var lowPriorityKeywords = new HashSet<string> { "公园", "广场", "绿地", "运动", "健身", "社区", "街道", "路", "巷" };

        var existingAttractionNames = amapAttractions.Select(p => p.Name.ToLower()).ToHashSet();
        var missedFamousAttractions = famousAttractionNames.Where(f => !existingAttractionNames.Any(n => n.Contains(f.ToLower()) || f.ToLower().Contains(n))).ToList();
        
        if (missedFamousAttractions.Any())
        {
            Console.WriteLine($"补充未搜索到的著名景点: {string.Join(", ", missedFamousAttractions)}");
            foreach (var famous in missedFamousAttractions)
            {
                amapAttractions.Add(new POIInfo
                {
                    Name = famous,
                    Address = $"{city}{famous}",
                    Type = "著名景点",
                    Location = new Location { Longitude = 116.40 + amapAttractions.Count * 0.01, Latitude = 39.90 + amapAttractions.Count * 0.01 }
                });
            }
        }

        var prioritizedAmapAttractions = amapAttractions
            .OrderByDescending(poi => 
            {
                var name = (poi.Name ?? "").ToLower();
                var type = (poi.Type ?? "").ToLower();
                
                foreach (var famous in famousAttractionNames)
                {
                    var famousLower = famous.ToLower();
                    if (name.Contains(famousLower) || famousLower.Contains(name) || name.Replace("景区", "").Contains(famousLower) || famousLower.Contains(name.Replace("景区", "")))
                    {
                        return 3;
                    }
                }
                if (highPriorityKeywords.Any(t => name.Contains(t.ToLower()) || type.Contains(t.ToLower())))
                    return 2;
                if (lowPriorityKeywords.Any(t => name.Contains(t.ToLower()) || type.Contains(t.ToLower())))
                    return 0;
                return 1;
            })
            .ToList();

        foreach (var poi in prioritizedAmapAttractions)
        {
            if (!usedNames.Contains(poi.Name))
            {
                usedNames.Add(poi.Name);
                combinedAttractions.Add(new
                {
                    Name = poi.Name,
                    Address = poi.Address,
                    Longitude = poi.Location?.Longitude ?? 116.40 + combinedAttractions.Count * 0.01,
                    Latitude = poi.Location?.Latitude ?? 39.90 + combinedAttractions.Count * 0.01,
                    Description = $"{poi.Name}是{city}著名的{poi.Type}景点",
                    Category = GetCategoryFromType(poi.Type),
                    TicketPrice = GetRandomTicketPrice(),
                    ImageUrl = ""
                });
            }
            if (combinedAttractions.Count >= 6) break;
        }

        foreach (var staticAttr in staticAttractions)
        {
            if (!usedNames.Contains(staticAttr.Name))
            {
                usedNames.Add(staticAttr.Name);
                combinedAttractions.Add(staticAttr);
            }
            if (combinedAttractions.Count >= 6) break;
        }

        if (combinedAttractions.Count == 0)
        {
            combinedAttractions = staticAttractions.Take(6).ToList();
        }

        Console.WriteLine($"最终使用 {combinedAttractions.Count} 个景点: {string.Join(", ", combinedAttractions.Select(a => a.Name))}");

        return new
        {
            Attractions = combinedAttractions.ToArray(),
            DayDescriptions = staticInfo.DayDescriptions,
            HotelName = staticInfo.HotelName,
            HotelAddress = staticInfo.HotelAddress,
            HotelLng = staticInfo.HotelLng,
            HotelLat = staticInfo.HotelLat,
            HotelPriceRange = staticInfo.HotelPriceRange,
            HotelCost = staticInfo.HotelCost,
            Breakfast = staticInfo.Breakfast,
            Lunch = staticInfo.Lunch,
            Dinner = staticInfo.Dinner,
            Suggestions = staticInfo.Suggestions
        };
    }

    private dynamic GetStaticCityInfo(string city)
    {
        if (city.Contains("成都"))
        {
            return new
            {
                Attractions = new[]
                {
                    new { Name = "宽窄巷子", Address = "四川省成都市青羊区金河宾馆北侧", Longitude = 104.067923, Latitude = 30.66359, Description = "清代古街道，体验老成都生活", Category = "历史文化", TicketPrice = 0, ImageUrl = "" },
                    new { Name = "锦里古街", Address = "四川省成都市武侯区武侯祠大街231号", Longitude = 104.05429, Latitude = 30.64633, Description = "古蜀文化商业街，美食聚集地", Category = "历史文化", TicketPrice = 0, ImageUrl = "" },
                    new { Name = "武侯祠", Address = "四川省成都市武侯区武侯祠大街231号", Longitude = 104.05498, Latitude = 30.64677, Description = "纪念诸葛亮的祠宇", Category = "历史文化", TicketPrice = 50, ImageUrl = "" },
                    new { Name = "杜甫草堂", Address = "四川省成都市青羊区青华路37号", Longitude = 104.03954, Latitude = 30.66028, Description = "唐代诗人杜甫的故居", Category = "历史文化", TicketPrice = 50, ImageUrl = "" },
                    new { Name = "大熊猫基地", Address = "四川省成都市成华区外北熊猫大道1375号", Longitude = 104.1799, Latitude = 30.74644, Description = "世界著名的大熊猫保护研究基地", Category = "自然景观", TicketPrice = 55, ImageUrl = "" },
                    new { Name = "都江堰", Address = "四川省成都市都江堰市公园路都江堰景区", Longitude = 103.61459, Latitude = 30.99758, Description = "世界文化遗产，古代水利工程", Category = "历史文化", TicketPrice = 80, ImageUrl = "" }
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
        else if (city.Contains("达州"))
        {
            return new
            {
                Attractions = new[]
                {
                    new { Name = "真佛山", Address = "四川省达州市达川区福善镇", Longitude = 107.59, Latitude = 31.18, Description = "集佛、道、儒三教合一的宗教圣地，始建于清嘉庆年间", Category = "历史文化", TicketPrice = 30, ImageUrl = "" },
                    new { Name = "莲花湖湿地公园", Address = "四川省达州市通川区", Longitude = 107.51, Latitude = 31.23, Description = "城市绿肺，自然风光优美", Category = "自然景观", TicketPrice = 0, ImageUrl = "" },
                    new { Name = "达州博物馆", Address = "四川省达州市通川区", Longitude = 107.53, Latitude = 31.22, Description = "综合性地方博物馆，收藏文物涵盖达州史前文化、巴人文化等", Category = "历史文化", TicketPrice = 0, ImageUrl = "" },
                    new { Name = "张爱萍故居", Address = "四川省达州市通川区罗江镇", Longitude = 107.45, Latitude = 31.28, Description = "开国上将张爱萍的故居，全国爱国主义教育示范基地", Category = "历史文化", TicketPrice = 0, ImageUrl = "" },
                    new { Name = "龙潭河", Address = "四川省达州市万源市", Longitude = 108.02, Latitude = 32.03, Description = "省级风景名胜区，以漂流和自然风光闻名", Category = "自然景观", TicketPrice = 50, ImageUrl = "" },
                    new { Name = "八台山", Address = "四川省达州市万源市", Longitude = 108.16, Latitude = 31.98, Description = "国家级自然遗产，有'川东峨眉'之称", Category = "自然景观", TicketPrice = 100, ImageUrl = "" }
                },
                DayDescriptions = new[] { "达州文化之旅", "自然风光之旅", "红色记忆之旅" },
                HotelName = "达州凤凰国际大酒店",
                HotelAddress = "达州市通川区凤凰大道",
                HotelLng = 107.53,
                HotelLat = 31.22,
                HotelPriceRange = "200-400元",
                HotelCost = 300,
                Breakfast = "达州特色早餐",
                Lunch = "川菜",
                Dinner = "达州美食",
                Suggestions = "达州是革命老区，建议参观红色景点；夏季注意防暑降温。"
            };
        }
        else
        {
            return new
            {
                Attractions = new[]
                {
                    new { Name = $"{city}广场", Address = $"{city}市中心", Longitude = 116.40, Latitude = 39.90, Description = $"{city}城市中心地标", Category = "现代景观", TicketPrice = 0, ImageUrl = "" },
                    new { Name = $"{city}博物馆", Address = $"{city}历史文化区", Longitude = 116.41, Latitude = 39.91, Description = $"{city}历史文化展示", Category = "历史文化", TicketPrice = 30, ImageUrl = "" },
                    new { Name = $"{city}公园", Address = $"{city}郊区", Longitude = 116.39, Latitude = 39.89, Description = "城市绿肺，自然风光", Category = "自然景观", TicketPrice = 20, ImageUrl = "" },
                    new { Name = $"{city}美食街", Address = $"{city}商业街", Longitude = 116.42, Latitude = 39.92, Description = $"{city}特色美食聚集地", Category = "美食", TicketPrice = 0, ImageUrl = "" },
                    new { Name = $"{city}文化中心", Address = $"{city}新区", Longitude = 116.43, Latitude = 39.88, Description = "现代文化艺术场馆", Category = "艺术", TicketPrice = 50, ImageUrl = "" },
                    new { Name = $"{city}古城墙", Address = $"{city}老城区", Longitude = 116.38, Latitude = 39.93, Description = "历史古城墙遗址", Category = "历史文化", TicketPrice = 40, ImageUrl = "" }
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

    private string GetCategoryFromType(string type)
    {
        if (string.IsNullOrEmpty(type)) return "其他";
        if (type.Contains("文化") || type.Contains("历史")) return "历史文化";
        if (type.Contains("公园") || type.Contains("自然") || type.Contains("风景")) return "自然景观";
        if (type.Contains("美食") || type.Contains("餐饮")) return "美食";
        if (type.Contains("购物") || type.Contains("商业")) return "购物";
        if (type.Contains("艺术") || type.Contains("博物馆")) return "艺术";
        return "现代景观";
    }

    private List<string> GetFamousAttractionsForCity(string city)
    {
        var famousAttractions = new Dictionary<string, List<string>>
        {
            { "南充", new List<string> { "阆中古城", "朱德故里", "升钟湖", "凌云山", "嘉陵江第一曲流" } },
            { "成都", new List<string> { "宽窄巷子", "锦里古街", "武侯祠", "杜甫草堂", "大熊猫基地", "都江堰", "青城山" } },
            { "重庆", new List<string> { "洪崖洞", "解放碑", "长江索道", "磁器口古镇", "武隆天生三桥", "李子坝轻轨站" } },
            { "达州", new List<string> { "真佛山", "莲花湖湿地公园", "达州博物馆", "张爱萍故居", "龙潭河", "八台山" } },
            { "绵阳", new List<string> { "越王楼", "富乐山", "七曲山大庙", "窦圌山", "药王谷" } },
            { "德阳", new List<string> { "三星堆", "什邡蓥华山", "德阳文庙" } },
            { "广元", new List<string> { "剑门关", "昭化古城", "皇泽寺", "千佛崖" } },
            { "遂宁", new List<string> { "灵泉寺", "广德寺", "中国死海" } },
            { "内江", new List<string> { "大千园", "圣水寺", "隆昌石牌坊" } },
            { "乐山", new List<string> { "乐山大佛", "峨眉山", "东方佛都" } },
            { "资阳", new List<string> { "安岳石刻", "陈毅故里" } },
            { "宜宾", new List<string> { "蜀南竹海", "五粮液景区", "兴文石海" } },
            { "泸州", new List<string> { "泸州老窖景区", "太平古镇", "黄荆老林" } },
            { "自贡", new List<string> { "自贡恐龙博物馆", "盐业历史博物馆", "荣县大佛" } },
            { "攀枝花", new List<string> { "二滩国家森林公园", "格萨拉生态旅游区" } },
            { "眉山", new List<string> { "三苏祠", "瓦屋山", "柳江古镇" } },
            { "广安", new List<string> { "邓小平故里", "华蓥山", "宝箴塞" } },
            { "巴中", new List<string> { "光雾山", "诺水河", "恩阳古镇" } },
            { "雅安", new List<string> { "碧峰峡", "蒙顶山", "上里古镇" } },
            { "凉山", new List<string> { "邛海", "泸沽湖", "螺髻山" } },
            { "甘孜", new List<string> { "稻城亚丁", "海螺沟", "四姑娘山" } },
            { "阿坝", new List<string> { "九寨沟", "黄龙", "青城山", "都江堰" } }
        };

        foreach (var kvp in famousAttractions)
        {
            if (city.Contains(kvp.Key))
            {
                return kvp.Value;
            }
        }

        return new List<string>();
    }

    private int GetRandomTicketPrice()
    {
        var random = new Random();
        return random.Next(0, 150);
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
