using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.AI;

/// <summary>
/// 通过 services/ai-gateway 调用 LLM：多模型路由、重试、熔断、截断守卫均由网关承载；
/// 本客户端只负责 HTTP 封装与错误透传（5xx/408/429/超时按 TransientHttpRetry 重试）。
/// </summary>
public class HttpLlmGateway : ILlmGateway, ITransientDependency
{
    private const int MaxAttempts = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiGatewayOptions _options;
    private readonly ILogger<HttpLlmGateway> _logger;

    public HttpLlmGateway(
        IHttpClientFactory httpClientFactory,
        IOptions<AiGatewayOptions> options,
        ILogger<HttpLlmGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpLlmGateway));
        if (!string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            // 网关契约：X-API-Key 头（AI_GATEWAY_API_TOKEN），不是 Authorization Bearer
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", _options.ApiToken);
        }

        var request = new
        {
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            mode = "instruct",
            business = "bid-compare"
        };

        using var response = await TransientHttpRetry.ExecuteAsync(
            async (attempt, ct) =>
            {
                var resp = await client.PostAsJsonAsync("v1/chat", request, JsonOptions, ct);
                // 最后一次尝试不抛瞬时异常：保留响应给上层解析网关错误信封（serviceCode 等诊断信息）
                return attempt < MaxAttempts
                    ? await TransientHttpRetry.ThrowIfTransientAsync(resp, "AI Gateway /v1/chat", ct)
                    : resp;
            },
            _logger,
            "AI Gateway /v1/chat",
            MaxAttempts,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await BuildGatewayExceptionAsync(response, cancellationToken);
        }

        var payload = await response.Content.ReadFromJsonAsync<ChatResponse>(JsonOptions, cancellationToken);
        return payload?.Text
            ?? throw new BusinessException(BidCompareErrorCodes.AiGatewayFailed)
                .WithData("reason", "AI Gateway 响应缺少 text");
    }

    private static async Task<BusinessException> BuildGatewayExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        string? code = null;
        string? message = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("code", out var c)) code = c.GetString();
            if (document.RootElement.TryGetProperty("message", out var m)) message = m.GetString();
        }
        catch (JsonException)
        {
            // 非 JSON 错误体：原样摘录
        }
        return new BusinessException(BidCompareErrorCodes.AiGatewayFailed)
            .WithData("statusCode", (int)response.StatusCode)
            .WithData("serviceCode", code ?? "")
            .WithData("message", message ?? (body.Length <= 512 ? body : body[..512]));
    }

    private class ChatResponse
    {
        public string? Text { get; set; }
        public string? FinishReason { get; set; }
        public JsonElement? Usage { get; set; }
        public string? UsedConfig { get; set; }
        public string? UsedModel { get; set; }
        public int? Attempts { get; set; }
        public double? LatencySeconds { get; set; }
        public string? CircuitBreakerState { get; set; }
    }
}
