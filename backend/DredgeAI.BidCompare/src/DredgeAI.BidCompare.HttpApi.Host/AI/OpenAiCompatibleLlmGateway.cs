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
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

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

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var content = payload.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content
            ?? throw new BusinessException(BidCompareErrorCodes.IrValidationFailed)
                .WithData("reason", "LLM 响应缺少 choices[0].message.content");
    }
}
