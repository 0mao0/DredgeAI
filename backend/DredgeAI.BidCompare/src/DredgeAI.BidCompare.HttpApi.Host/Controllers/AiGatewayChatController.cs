using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

/// <summary>POST /api/ai-gateway/chat/stream：前端统一问答端点，SSE 透传 services/ai-gateway。</summary>
[Route("api/ai-gateway")]
public class AiGatewayChatController : AbpControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public AiGatewayChatController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("chat/stream")]
    public async Task<System.IO.Stream> ChatStreamAsync([FromBody] ChatStreamRequest input)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpLlmGateway));
        var upstream = await client.PostAsJsonAsync(
            "v1/chat/stream",
            input,
            JsonOptions,
            HttpContext.RequestAborted);
        upstream.EnsureSuccessStatusCode();

        HttpContext.Response.ContentType = "text/event-stream";
        HttpContext.Response.Headers.CacheControl = "no-cache";
        var stream = await upstream.Content.ReadAsStreamAsync(HttpContext.RequestAborted);
        return new OwnedStream(stream, upstream);
    }
}

public class ChatStreamRequest
{
    public List<ChatStreamMessage> Messages { get; set; } = new();
    public string? Mode { get; set; }
    public string? ConfigName { get; set; }
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public string? Business { get; set; }
}

public class ChatStreamMessage
{
    public string Role { get; set; } = default!;
    public JsonElement? Content { get; set; }
}
