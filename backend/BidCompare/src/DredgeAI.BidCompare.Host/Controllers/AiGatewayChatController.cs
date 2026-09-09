using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Volo.Abp;

namespace DredgeAI.BidCompare.Controllers;

/// <summary>AI 网关接口（流式问答）</summary>
/// <remarks>POST /api/bidcompare/ai-gateway/chat/stream：前端统一问答端点，SSE 透传 services/ai-gateway。</remarks>
[Authorize]
[Route("api/bidcompare/ai-gateway")]
[RemoteService(Name = BidCompareRemoteServiceConsts.RemoteServiceName)]
[Area(BidCompareRemoteServiceConsts.ModuleName)]
[Tags("AI 网关")]
public class AiGatewayChatController : BidCompareController
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiGatewayOptions _options;

    public AiGatewayChatController(
        IHttpClientFactory httpClientFactory,
        IOptions<AiGatewayOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    /// <summary>流式对话：将请求透传给 AI 网关并透传其 SSE 响应流</summary>
    /// <param name="input">对话请求：消息列表与生成参数</param>
    /// <returns>text/event-stream 响应流</returns>
    [HttpPost("chat/stream")]
    public async Task<System.IO.Stream> ChatStreamAsync([FromBody] ChatStreamRequest input)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpLlmGateway));
        if (!string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            // 网关契约：X-API-Key 头（AI_GATEWAY_API_TOKEN），与 HttpLlmGateway 一致
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", _options.ApiToken);
        }
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
