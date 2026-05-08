using System.Text;
using System.Text.Json;
using AiTravelAgent.Configuration;
using AiTravelAgent.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AiTravelAgent.Services;

public class ChatService
{
    private const string SystemPrompt = @"你是一个专业且贴心的私人旅行管家「旅途星辰AI」。

你当前正在为用户提供关于一份 **已生成的旅行计划** 的答疑服务。
用户可能会问你关于行程中的景点、酒店、餐饮、天气、交通、门票、费用等任何细节问题。

请根据下方提供的【当前旅行计划】JSON 上下文来回答用户的问题。
回答规则：
1. 如果行程数据中包含相关信息，请精确引用并给出详细回答。
2. 如果行程数据中没有明确信息，可以基于常识进行合理推断，但需说明'行程中未提供该信息，以下是建议'。
3. 回答要有温度、条理清晰，适当使用 emoji 增加亲切感。
4. 回答尽量简洁，控制在200字以内，除非用户要求详细展开。
5. 使用中文回答。";

    public static async Task<string> ChatWithTripContextAsync(string message, Dictionary<string, object> tripPlan, List<ChatMessage>? history = null)
    {
        var settings = ConfigurationLoader.LoadSettings();

        if (string.IsNullOrEmpty(settings.OpenAiApiKey))
        {
            return "抱歉，AI 服务尚未配置 API Key，请先在设置页面中完成配置。";
        }

        var tripPlanJson = JsonConvert.SerializeObject(tripPlan, Formatting.Indented);
        var contextMessage = $"【当前旅行计划】\n```json\n{tripPlanJson}\n```";

        var messages = new List<(string role, string content)>
        {
            ("system", SystemPrompt),
            ("user", contextMessage)
        };

        if (history != null)
        {
            foreach (var item in history)
            {
                messages.Add((item.Role, item.Content));
            }
        }

        messages.Add(("user", message));

        return await LLMService.GetInstance().ChatCompletionWithHistoryAsync(messages, maxTokens: 1024, temperature: 0.7);
    }
}
