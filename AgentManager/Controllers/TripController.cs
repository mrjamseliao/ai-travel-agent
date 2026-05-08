using System.Collections.Concurrent;
using AiTravelAgent.Agents;
using AiTravelAgent.Models;
using AiTravelAgent.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiTravelAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TripController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, TripTaskState> _tasks = new();

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
    public IActionResult GetTripHistory(int limit = 10)
    {
        var items = _tasks.Values
            .Where(t => t.Status == "completed" && t.Result != null)
            .OrderByDescending(t => t.Progress)
            .Take(limit)
            .Select(t => new
            {
                plan_id = t.PlanId,
                task_id = t.TaskId,
                city = (t.Result?.GetType().GetProperty("Data")?.GetValue(t.Result) as TripPlan)?.City ?? "",
                travel_days = (t.Result?.GetType().GetProperty("Data")?.GetValue(t.Result) as TripPlan)?.Days.Count ?? 0,
                overall_suggestions = (t.Result?.GetType().GetProperty("Message")?.GetValue(t.Result) as TripPlanResponse)?.Message ?? ""
            })
            .ToList();

        return Ok(new { items });
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
