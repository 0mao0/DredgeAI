using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
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
        var messages = new object[]
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = userPrompt }
        };
        return await PostChatAsync(messages, cancellationToken);
    }

    public async IAsyncEnumerable<string> CompleteStreamAsync(
        string systemPrompt,
        string userPrompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = new object[]
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = userPrompt }
        };
        var client = _httpClientFactory.CreateClient(nameof(HttpLlmGateway));
        if (!string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", _options.ApiToken);
        }

        var request = new
        {
            messages,
            mode = "instruct",
            business = "bid-compare"
        };

        using var response = await client.PostAsJsonAsync("v1/chat/stream", request, JsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await BuildGatewayExceptionAsync(response, cancellationToken);
        }

        await using var upstream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(upstream);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!line.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }
            using var document = JsonDocument.Parse(line[6..]);
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
            if (type == "delta"
                && root.TryGetProperty("text", out var textElement)
                && textElement.GetString() is { Length: > 0 } text)
            {
                yield return text;
            }
            else if (type == "error")
            {
                throw BuildStreamError(root, _logger);
            }
        }
    }

    private static BusinessException BuildStreamError(JsonElement root, ILogger<HttpLlmGateway> logger)
    {
        var message = "AI Gateway 流式响应错误";
        var errorCode = "";
        if (root.TryGetProperty("error", out var error))
        {
            if (error.TryGetProperty("type", out var t)) errorCode = t.GetString() ?? "";
            if (error.TryGetProperty("message", out var m)) message = m.GetString() ?? message;
        }
        logger.LogWarning("AI Gateway 流式响应错误事件：{Code} {Message}", errorCode, message);
        return new BusinessException(BidCompareErrorCodes.AiGatewayFailed)
            .WithData("serviceCode", errorCode)
            .WithData("message", message);
    }

    public async Task<string> CompleteMultimodalAsync(
        string systemPrompt,
        string text,
        IReadOnlyList<LlmImageInput> images,
        CancellationToken cancellationToken = default)
    {
        var contentParts = new List<object> { new { type = "text", text } };
        foreach (var image in images)
        {
            contentParts.Add(new
            {
                type = "image_url",
                image_url = new { url = $"data:{image.MimeType};base64,{image.Base64Data}" }
            });
        }

        var messages = new object[]
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = contentParts }
        };
        return await PostChatAsync(messages, cancellationToken);
    }

    private async Task<string> PostChatAsync(object[] messages, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpLlmGateway));
        if (!string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            // 网关契约：X-API-Key 头（AI_GATEWAY_API_TOKEN），不是 Authorization Bearer
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", _options.ApiToken);
        }

        var request = new
        {
            messages,
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
