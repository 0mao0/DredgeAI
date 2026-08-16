using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.AI;

/// <summary>OpenAI 兼容协议实现：POST {Endpoint}/chat/completions。</summary>
public class OpenAiCompatibleLlmGateway : ILlmGateway, ITransientDependency
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LlmOptions _options;

    public OpenAiCompatibleLlmGateway(IHttpClientFactory httpClientFactory, IOptions<LlmOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(nameof(OpenAiCompatibleLlmGateway));
        client.Timeout = System.TimeSpan.FromSeconds(_options.TimeoutSeconds);
        // ApiKey 为空时不附加 Bearer 头（本地/内网模型网关常无鉴权）
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        var request = new
        {
            model = _options.Model,
            temperature = 0.2,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        using var response = await client.PostAsJsonAsync(
            $"{_options.Endpoint.TrimEnd('/')}/chat/completions", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            using var payload = JsonDocument.Parse(body);
            var content = payload.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return content
                ?? throw new BusinessException(BidCompareErrorCodes.IrValidationFailed)
                    .WithData("reason", "LLM 响应缺少 choices[0].message.content");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            // 错误体/结构漂移不应抛 KeyNotFoundException，转业务异常并附原始 body 摘要
            var excerpt = body.Length <= 512 ? body : body[..512];
            throw new BusinessException(BidCompareErrorCodes.IrValidationFailed)
                .WithData("reason", $"LLM 响应结构无法解析：{ex.Message}")
                .WithData("body", excerpt);
        }
    }
}
