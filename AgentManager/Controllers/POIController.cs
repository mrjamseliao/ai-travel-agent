using AiTravelAgent.Models;
using AiTravelAgent.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiTravelAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class POIController : ControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> SearchPOI([FromQuery] string keywords, [FromQuery] string city, [FromQuery] bool citylimit = true)
    {
        try
        {
            var amap = AmapService.GetInstance();
            var results = await amap.SearchPOIAsync(keywords, city, citylimit);

            return Ok(new POISearchResponse
            {
                Success = true,
                Message = "搜索成功",
                Data = results
            });
        }
        catch (Exception ex)
        {
            return Ok(new POISearchResponse
            {
                Success = false,
                Message = $"搜索失败: {ex.Message}",
                Data = new List<POIInfo>()
            });
        }
    }

    [HttpGet("geocode")]
    public async Task<IActionResult> Geocode([FromQuery] string address, [FromQuery] string city)
    {
        try
        {
            var amap = AmapService.GetInstance();
            var location = await amap.GeocodeAsync(address, city);

            if (location != null)
            {
                return Ok(new
                {
                    success = true,
                    data = location
                });
            }

            return Ok(new
            {
                success = false,
                message = "地理编码失败"
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                success = false,
                message = $"地理编码失败: {ex.Message}"
            });
        }
    }
}
