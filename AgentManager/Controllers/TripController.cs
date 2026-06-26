using System.Collections.Concurrent;
using AiTravelAgent.Agents;
using AiTravelAgent.Models;
using AiTravelAgent.Repositories;
using AiTravelAgent.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiTravelAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TripController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, TripTaskState> _tasks = new();
    private readonly DatabaseService _dbService;
    private readonly AttractionRepository _attractionRepo;

    public TripController(DatabaseService dbService)
    {
        _dbService = dbService;
        _attractionRepo = new AttractionRepository(dbService);
    }

    [HttpPost("plan")]
    public async Task<IActionResult> PlanTrip([FromBody] TripRequest request)
    {
        var taskId = Guid.NewGuid().ToString("N")[..8];

        var state = new TripTaskState
        {
            TaskId = taskId,
            PlanId = taskId,
            Status = "processing",
            Stage = "submitted",
            Progress = 0,
            Message = "任务已提交，等待执行...",
            RequestPayload = request
        };

        _tasks[taskId] = state;

        Console.WriteLine($"\n{new string('=', 60)}");
        Console.WriteLine($"收到旅行规划请求 (task_id={taskId}):");
        Console.WriteLine($"   城市: {request.City}");
        Console.WriteLine($"   日期: {request.StartDate} - {request.EndDate}");
        Console.WriteLine($"   天数: {request.TravelDays}");
        Console.WriteLine($"{new string('=', 60)}\n");

        _ = Task.Run(async () =>
        {
            try
            {
                await UpdateTaskState(taskId, status: "processing", stage: "initializing", progress: 10, message: "正在获取多智能体系统实例...");

                var agent = TripPlannerAgent.GetInstance();

                var tripPlan = await agent.PlanTripAsync(request, (stage, message, progress) =>
                {
                    UpdateTaskState(taskId, status: "processing", stage: stage, progress: progress, message: message);
                });

                await UpdateTaskState(taskId, status: "processing", stage: "matching_data", progress: 90, message: "正在匹配景点数据...");

                await MatchAttractionData(tripPlan, request.City);

                await UpdateTaskState(taskId, status: "processing", stage: "graph_building", progress: 95, message: "正在构建知识图谱...");

                var graphData = KnowledgeGraphService.BuildKnowledgeGraph(tripPlan, request.Language ?? "zh");

                var result = new TripPlanResponse
                {
                    Success = true,
                    Message = "旅行计划生成成功",
                    PlanId = taskId,
                    Data = tripPlan,
                    GraphData = graphData
                };

                await SaveTripToHistory(taskId, request, tripPlan);

                Console.WriteLine($"任务 {taskId} 完成");
                await UpdateTaskState(taskId, status: "completed", stage: "completed", progress: 100, message: "旅行计划生成成功", result: result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"任务 {taskId} 失败: {ex.Message}");
                await UpdateTaskState(taskId, status: "failed", stage: "failed", progress: 100, message: ex.Message, error: ex.Message);
            }
        });

        return Ok(new
        {
            task_id = taskId,
            plan_id = taskId,
            status = "processing",
            ws_url = $"/api/trip/ws/{taskId}",
            message = $"任务已提交，可通过 WebSocket /api/trip/ws/{taskId} 实时订阅状态"
        });
    }

    private async Task UpdateTaskState(string taskId, string? status = null, string? stage = null, int? progress = null, string? message = null, object? result = null, string? error = null)
    {
        if (_tasks.TryGetValue(taskId, out var state))
        {
            if (status != null) state.Status = status;
            if (stage != null) state.Stage = stage;
            if (progress != null) state.Progress = progress.Value;
            if (message != null) state.Message = message;
            if (result != null) state.Result = result;
            if (error != null) state.Error = error;

            if (state.Result is TripPlanResponse tripResult)
            {
                state.Result = new
                {
                    success = tripResult.Success,
                    message = tripResult.Message,
                    plan_id = tripResult.PlanId,
                    data = tripResult.Data,
                    graph_data = tripResult.GraphData
                };
            }
        }

        await Task.CompletedTask;
    }

    [HttpGet("status/{taskId}")]
    public IActionResult GetTaskStatus(string taskId)
    {
        if (!_tasks.TryGetValue(taskId, out var task))
        {
            return NotFound(new { error = "任务不存在" });
        }

        if (task.Status == "completed")
        {
            return Ok(new
            {
                task_id = taskId,
                plan_id = task.PlanId,
                status = "completed",
                result = task.Result
            });
        }

        if (task.Status == "failed")
        {
            return Ok(new
            {
                task_id = taskId,
                plan_id = task.PlanId,
                status = "failed",
                error = task.Error,
                request_payload = task.RequestPayload
            });
        }

        return Ok(new
        {
            task_id = taskId,
            plan_id = task.PlanId,
            status = "processing",
            stage = task.Stage,
            progress = task.Progress,
            progress_text = task.Message
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetTripHistory(int limit = 10)
    {
        try
        {
            var repo = new TripHistoryRepository(_dbService);
            var histories = await repo.GetAllAsync(limit);
            
            var items = histories.Select(h => new
            {
                id = h.Id,
                plan_id = h.TaskId ?? h.Id,
                task_id = h.TaskId,
                city = h.City ?? "",
                days = h.Days ?? "",
                travel_days = int.TryParse(h.Days, out var d) ? d : 0,
                date = $"{h.DepartureDate}",
                title = $"{h.City} {h.Days}天旅行",
                created_at = h.CreateTime,
                status = h.Status,
                plan_data = h.PlanData
            }).ToList();

            return Ok(new { items });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取旅行历史失败: {ex.Message}");
            return Ok(new { items = new List<object>() });
        }
    }

    [HttpDelete("history/{id}")]
    public async Task<IActionResult> DeleteTripHistory(string id)
    {
        try
        {
            var repo = new TripHistoryRepository(_dbService);
            var deleted = await repo.DeleteAsync(id);
            return Ok(new { success = deleted });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"删除旅行历史失败: {ex.Message}");
            return Ok(new { success = false });
        }
    }

    [HttpGet("history/{id}")]
    public async Task<IActionResult> GetTripById(string id)
    {
        try
        {
            var repo = new TripHistoryRepository(_dbService);
            var history = await repo.GetByIdAsync(id);
            
            if (history == null)
            {
                return NotFound(new { success = false, message = "计划不存在" });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    id = history.Id,
                    plan_id = history.TaskId ?? history.Id,
                    city = history.City,
                    days = history.Days,
                    date = history.DepartureDate,
                    title = $"{history.City} {history.Days}天旅行",
                    created_at = history.CreateTime,
                    plan_data = history.PlanData
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取旅行计划失败: {ex.Message}");
            return Ok(new { success = false, message = ex.Message });
        }
    }

    private async Task MatchAttractionData(TripPlan tripPlan, string city)
    {
        try
        {
            var allAttractions = await _attractionRepo.GetAllAsync();
            
            var cityAttractions = allAttractions
                .Where(a => a.City.Contains(city) || city.Contains(a.City))
                .ToList();

            Console.WriteLine($"在数据库中找到 {cityAttractions.Count} 个{city}相关的景点");

            foreach (var day in tripPlan.Days)
            {
                foreach (var attraction in day.Attractions)
                {
                    var matched = cityAttractions.FirstOrDefault(a => 
                        IsNameMatch(a.Name, attraction.Name));

                    if (matched == null)
                    {
                        Console.WriteLine($"未匹配到景点: {attraction.Name}, 数据库中有: {string.Join(", ", cityAttractions.Select(a => a.Name))}");
                    }

                    if (matched != null)
                    {
                        if (!string.IsNullOrEmpty(matched.CoverImage))
                        {
                            if (IsValidImageUrl(matched.CoverImage))
                            {
                                attraction.ImageUrl = matched.CoverImage;
                                Console.WriteLine($"匹配景点图片(URL): {attraction.Name} -> {matched.CoverImage}");
                            }
                            else
                            {
                                var imageUrl = await SaveBase64ImageAndGetUrl(matched.CoverImage, matched.Name);
                                if (!string.IsNullOrEmpty(imageUrl))
                                {
                                    attraction.ImageUrl = imageUrl;
                                    Console.WriteLine($"匹配景点图片(转换后): {attraction.Name} -> {imageUrl}");
                                }
                                else
                                {
                                    Console.WriteLine($"景点图片转换失败: {attraction.Name}");
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(matched.Intro))
                        {
                            attraction.Description = matched.Intro;
                            Console.WriteLine($"匹配景点简介: {attraction.Name} -> {matched.Intro.Substring(0, Math.Min(50, matched.Intro.Length))}...");
                        }
                        if (!string.IsNullOrEmpty(matched.Category))
                        {
                            attraction.Category = matched.Category;
                        }
                        if (!string.IsNullOrEmpty(matched.Address))
                        {
                            attraction.Address = matched.Address;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"匹配景点数据失败: {ex.Message}");
        }
    }

    private bool IsValidImageUrl(string imageData)
    {
        if (string.IsNullOrEmpty(imageData))
            return false;
        return imageData.StartsWith("http://") || imageData.StartsWith("https://");
    }

    private async Task<string?> SaveBase64ImageAndGetUrl(string base64Data, string attractionName)
    {
        try
        {
            var base64Parts = base64Data.Split(',');
            if (base64Parts.Length < 2)
                return null;

            var header = base64Parts[0];
            var base64Content = base64Parts[1];

            string extension;
            if (header.Contains("image/png"))
                extension = ".png";
            else if (header.Contains("image/jpeg") || header.Contains("image/jpg"))
                extension = ".jpg";
            else if (header.Contains("image/webp"))
                extension = ".webp";
            else if (header.Contains("image/gif"))
                extension = ".gif";
            else
                return null;

            var cleanName = SanitizeFileName(attractionName);
            var fileName = $"{cleanName}_{Guid.NewGuid().ToString("N")[..8]}{extension}";
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "attractions");
            
            if (!Directory.Exists(uploadsDir))
                Directory.CreateDirectory(uploadsDir);

            var filePath = Path.Combine(uploadsDir, fileName);
            var imageBytes = Convert.FromBase64String(base64Content);
            
            await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);
            
            return $"/uploads/attractions/{fileName}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存Base64图片失败: {ex.Message}");
            return null;
        }
    }

    private string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var result = new string(name.Where(c => !invalidChars.Contains(c)).ToArray());
        return result.Length > 50 ? result.Substring(0, 50) : result;
    }

    private bool IsNameMatch(string dbName, string planName)
    {
        if (string.IsNullOrEmpty(dbName) || string.IsNullOrEmpty(planName))
            return false;

        var db = dbName.ToLower().Trim();
        var plan = planName.ToLower().Trim();

        if (db == plan) return true;
        if (db.Contains(plan)) return true;
        if (plan.Contains(db)) return true;

        var dbClean = RemoveSuffix(db);
        var planClean = RemoveSuffix(plan);

        if (dbClean == planClean) return true;
        if (dbClean.Contains(planClean)) return true;
        if (planClean.Contains(dbClean)) return true;

        return false;
    }

    private string RemoveSuffix(string name)
    {
        var suffixes = new[] { "景区", "景点", "公园", "广场", "古镇", "古城", "纪念馆", "故居", "寺", "庙", "祠", "楼", "塔", "山", "湖", "河", "江", "岛", "洞", "博物馆", "文化园", "风景区", "国家地质公园", "世界遗产", "旅游区", "度假区", "生态园", "植物园", "动物园", "森林公园", "湿地公园", "自然保护区", "地质公园", "水利风景区", "风景名胜区", "文化遗产", "遗址公园", "考古遗址公园", "保护研究基地", "繁育研究基地", "研究基地", "基地", "中心", "馆", "院", "园", "苑", "寨", "村", "庄", "镇", "街", "巷", "坊", "桥", "路", "大道", "路", "道", "巷", "里", "弄", "胡同", "沟", "峡", "谷", "滩", "湾", "礁", "崖", "峰", "岭", "岩", "坡", "坪", "坝", "台", "塬", "梁", "峁", "壑", "洞", "窟", "泉", "瀑", "池", "潭", "溪", "涧", "沟", "渠", "塘", "堰", "井", "泉", "温泉", "浴场", "瀑布", "峡谷", "溶洞", "森林", "草原", "沙漠", "戈壁", "冰川", "雪山", "湿地", "沼泽", "湖泊", "海洋", "海岸", "海岛", "港湾", "河口", "三角洲", "平原", "高原", "盆地", "丘陵", "山地", "高原", "台地", "谷地", "洼地", "冲积扇", "沙丘", "雅丹", "丹霞", "喀斯特", "花岗岩", "玄武岩", "火山", "地震", "地热", "温泉", "矿泉", "盐湖", "盐池", "泥火山", "间歇泉", "地热田", "热泉", "冷泉", "矿泉", "温泉", "喷泉", "涌泉", "瀑布", "跌水", "水潭", "水池", "水库", "湖泊", "池塘", "沼泽", "湿地", "河流", "运河", "渠道", "溪涧", "沟谷", "峡谷", "裂谷", "地缝", "天坑", "漏斗", "溶洞", "暗河", "地下河", "地下湖", "地下海", "冰洞", "风洞", "雨洞", "雾洞", "云洞", "水帘洞", "天生桥", "天生桥洞", "穿洞", "天窗", "竖井", "塌陷坑", "溶蚀洼地", "坡立谷", "盲谷", "断头河", "牛轭湖", "离堆山", "壶穴", "深切曲流", "峡谷曲流", "嶂谷", "隘谷", "峡谷", "宽谷", "河谷", "河漫滩", "阶地", "三角洲", "冲积扇", "洪积扇", "沙丘", "沙垄", "沙嘴", "沙坝", "潟湖", "海蚀崖", "海蚀柱", "海蚀洞", "海蚀平台", "海积平原", "海滩", "潮间带", "珊瑚礁", "红树林", "河口湾", "峡湾", "半岛", "岛屿", "群岛", "列岛", "岩礁", "沙洲", "暗沙", "浅滩", "深槽", "海沟", "海岭", "洋盆", "洋中脊", "裂谷", "转换断层", "板块边界", "俯冲带", "碰撞带", "缝合线", "地缝合线", "火山弧", "岛弧", "海沟", "弧后盆地", "边缘海", "陆缘海", "内陆海", "地中海", "红海", "黑海", "波罗的海", "加勒比海", "南海", "东海", "黄海", "渤海", "日本海", "鄂霍次克海", "白令海", "阿拉斯加湾", "加利福尼亚湾", "墨西哥湾", "加勒比海", "大西洋", "印度洋", "太平洋", "北冰洋", "南极洲", "北极", "赤道", "回归线", "极圈", "经线", "纬线", "时区", "国际日期变更线", "本初子午线", "赤道无风带", "副热带高压", "西风带", "极地东风带", "信风带", "季风", "台风", "飓风", "龙卷风", "雷暴", "冰雹", "雪暴", "沙尘暴", "雾", "霾", "霜", "露", "雨", "雪", "霰", "冰", "冻雨", "雨夹雪", "毛毛雨", "小雨", "中雨", "大雨", "暴雨", "大暴雨", "特大暴雨", "小雪", "中雪", "大雪", "暴雪", "大暴雪", "特大暴雪", "雾", "浓雾", "强浓雾", "霾", "轻雾", "吹雪", "雪暴", "雷暴", "闪电", "冰雹", "飑", "龙卷", "尘卷风", "沙尘暴", "扬沙", "浮尘", "烟幕", "霾", "霜", "雾凇", "雨凇", "结冰", "积雪", "积冰", "凌汛", "洪水", "涝灾", "旱灾", "台风", "风暴潮", "海啸", "海冰", "赤潮", "厄尔尼诺", "拉尼娜", "全球变暖", "气候变化", "温室效应", "臭氧洞", "酸雨", "光化学烟雾", "大气污染", "水污染", "土壤污染", "固体废物污染", "噪声污染", "放射性污染", "电磁辐射污染", "热污染", "光污染", "生物污染", "病原体污染", "农药污染", "化肥污染", "重金属污染", "有机物污染", "无机物污染", "悬浮物污染", "溶解物污染", "胶体污染", "细菌污染", "病毒污染", "寄生虫污染", "真菌污染", "藻类污染", "赤潮", "水华", "富营养化", "酸化", "碱化", "盐化", "硬化", "沙漠化", "石漠化", "水土流失", "土壤侵蚀", "土地退化", "土地沙化", "土地盐碱化", "土地荒漠化", "森林退化", "草原退化", "湿地退化", "生物多样性减少", "物种灭绝", "濒危物种", "珍稀物种", "特有物种", "外来物种", "入侵物种", "生态破坏", "生态失衡", "生态危机", "生态修复", "生态重建", "生态恢复", "生态保护", "生态文明", "可持续发展", "绿色发展", "低碳发展", "循环经济", "绿色经济", "生态经济", "环境经济", "资源经济", "人口经济", "区域经济", "城市经济", "农村经济", "工业经济", "农业经济", "服务业经济", "数字经济", "知识经济", "信息经济", "网络经济", "共享经济", "平台经济", "新经济", "实体经济", "虚拟经济", "泡沫经济", "市场经济", "计划经济", "混合经济", "开放经济", "封闭经济", "外向型经济", "内向型经济", "粗放型经济", "集约型经济", "增长型经济", "衰退型经济", "停滞型经济", "复苏型经济", "繁荣型经济", "萧条型经济", "危机型经济", "转型经济", "新兴经济", "传统经济", "现代经济", "后现代经济", "全球化经济", "区域化经济", "本地化经济", "国际化经济", "本土化经济", "多元化经济", "专业化经济", "规模化经济", "产业化经济", "集群化经济", "网络化经济", "信息化经济", "智能化经济", "自动化经济", "数字化经济", "可视化经济", "可追溯经济", "可循环经济", "可再生经济", "可替代经济", "可持续经济", "可协调经济", "可发展经济", "可创新经济", "可创业经济", "可就业经济", "可消费经济", "可投资经济", "可贸易经济", "可金融经济", "可货币经济", "可财政经济", "可税收经济", "可预算经济", "可审计经济", "可监管经济", "可调控经济", "可管理经济", "可治理经济", "可服务经济", "可保障经济", "可福利经济", "可民生经济", "可社会经济", "可政治经济", "可文化经济", "可教育经济", "可科技经济", "可创新经济", "可人才经济", "可人力资源经济", "可自然资源经济", "可环境资源经济", "可生态资源经济", "可旅游资源经济", "可文化资源经济", "可历史资源经济", "可文物资源经济", "可艺术资源经济", "可体育资源经济", "可娱乐资源经济", "可休闲资源经济", "可健康资源经济", "可医疗资源经济", "可养老资源经济", "可教育资源经济", "可科技资源经济", "可创新资源经济", "可人才资源经济", "可人力资源经济", "可自然资源经济", "可环境资源经济", "可生态资源经济", "可旅游资源经济", "可文化资源经济", "可历史资源经济", "可文物资源经济", "可艺术资源经济", "可体育资源经济", "可娱乐资源经济", "可休闲资源经济", "可健康资源经济", "可医疗资源经济", "可养老资源经济", "可教育资源经济", "可科技资源经济", "可创新资源经济", "可人才资源经济", "可人力资源经济", "可自然资源经济", "可环境资源经济", "可生态资源经济", "可旅游资源经济", "可文化资源经济", "可历史资源经济", "可文物资源经济", "可艺术资源经济", "可体育资源经济", "可娱乐资源经济", "可休闲资源经济", "可健康资源经济", "可医疗资源经济", "可养老资源经济", "可教育资源经济", "可科技资源经济", "可创新资源经济", "可人才资源经济", "可人力资源经济", "可自然资源经济", "可环境资源经济", "可生态资源经济", "可旅游资源经济", "可文化资源经济", "可历史资源经济", "可文物资源经济", "可艺术资源经济", "可体育资源经济", "可娱乐资源经济", "可休闲资源经济", "可健康资源经济", "可医疗资源经济", "可养老资源经济" };
        foreach (var suffix in suffixes)
        {
            name = name.Replace(suffix, "");
        }
        return name.Trim();
    }

    private async Task SaveTripToHistory(string taskId, TripRequest request, TripPlan tripPlan)
    {
        try
        {
            var repo = new TripHistoryRepository(_dbService);
            
            var history = new TripHistory
            {
                Id = Guid.NewGuid().ToString(),
                TaskId = taskId,
                City = request.City,
                Days = request.TravelDays.ToString(),
                Preferences = string.Join(",", request.Preferences),
                Budget = request.FreeTextInput,
                DepartureDate = request.StartDate,
                Status = "completed",
                PlanData = System.Text.Json.JsonSerializer.Serialize(tripPlan),
                CreateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            await repo.CreateAsync(history);
            Console.WriteLine($"旅行计划已保存到数据库 (task_id={taskId})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存旅行计划到数据库失败: {ex.Message}");
        }
    }

    [HttpGet("health")]
    public IActionResult HealthCheck()
    {
        try
        {
            var agent = TripPlannerAgent.GetInstance();
            return Ok(new
            {
                status = "healthy",
                service = "trip-planner",
                agent_name = "TripPlannerAgent"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { status = "unhealthy", error = ex.Message });
        }
    }

    private class TripTaskState
    {
        public string TaskId { get; set; } = "";
        public string PlanId { get; set; } = "";
        public string Status { get; set; } = "";
        public string Stage { get; set; } = "";
        public int Progress { get; set; }
        public string Message { get; set; } = "";
        public object? Result { get; set; }
        public string? Error { get; set; }
        public object? RequestPayload { get; set; }
    }
}
