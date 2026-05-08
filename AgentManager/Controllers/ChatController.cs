using AiTravelAgent.Models;
using AiTravelAgent.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiTravelAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    [HttpPost("trip")]
    public async Task<IActionResult> ChatWithTrip([FromBody] TripChatRequest request)
    {
        try
        {
            var reply = await ChatService.ChatWithTripContextAsync(
                request.Message,
                request.TripPlan,
                request.History
            );

            return Ok(new TripChatResponse
            {
                Success = true,
                Reply = reply
            });
        }
        catch (Exception ex)
        {
            return Ok(new TripChatResponse
            {
                Success = false,
                Reply = $"抱歉，AI 服务暂时出现问题，请稍后重试 🙏"
            });
        }
    }
}
