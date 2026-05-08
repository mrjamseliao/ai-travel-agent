using AiTravelAgent.Models;
using AiTravelAgent.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiTravelAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MapController : ControllerBase
{
    [HttpGet("weather")]
    public async Task<IActionResult> GetWeather([FromQuery] string city)
    {
        try
        {
            var amap = AmapService.GetInstance();
            var weatherList = await amap.GetWeatherAsync(city);

            return Ok(new WeatherResponse
            {
                Success = true,
                Message = "查询成功",
                Data = weatherList
            });
        }
        catch (Exception ex)
        {
            return Ok(new WeatherResponse
            {
                Success = false,
                Message = $"查询失败: {ex.Message}",
                Data = new List<WeatherInfo>()
            });
        }
    }

    [HttpGet("route")]
    public async Task<IActionResult> PlanRoute(
        [FromQuery] string originAddress,
        [FromQuery] string destinationAddress,
        [FromQuery] string routeType = "walking",
        [FromQuery] string? originCity = null,
        [FromQuery] string? destinationCity = null)
    {
        try
        {
            var amap = AmapService.GetInstance();
            var route = await amap.PlanRouteAsync(originAddress, destinationAddress, routeType);

            if (route != null)
            {
                return Ok(new RouteResponse
                {
                    Success = true,
                    Message = "路线规划成功",
                    Data = route
                });
            }

            return Ok(new RouteResponse
            {
                Success = false,
                Message = "路线规划失败"
            });
        }
        catch (Exception ex)
        {
            return Ok(new RouteResponse
            {
                Success = false,
                Message = $"路线规划失败: {ex.Message}"
            });
        }
    }
}
