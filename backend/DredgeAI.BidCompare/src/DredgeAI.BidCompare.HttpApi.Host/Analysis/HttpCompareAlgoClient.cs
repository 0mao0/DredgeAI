using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.Analysis;

/// <summary>
/// 算法服务 HTTP client：POST {BaseUrl}/analyze/similarity|pricing|metadata，请求体为 AnGIneer 原始产物。
/// 5xx/408/429/超时带有限次指数退避重试；非 2xx 错误信封（{"code","message","details"}）透传为业务异常。
/// </summary>
public class HttpCompareAlgoClient : ICompareAlgoClient, ITransientDependency
{
    private const int MaxAttempts = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AlgoServiceOptions _options;
    private readonly ILogger<HttpCompareAlgoClient> _logger;

    public HttpCompareAlgoClient(
        IHttpClientFactory httpClientFactory,
        IOptions<AlgoServiceOptions> options,
        ILogger<HttpCompareAlgoClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public Task<IReadOnlyList<AlgoEvidence>> AnalyzeSimilarityAsync(string taskId, IReadOnlyList<AlgoRawDocument> documents, CancellationToken cancellationToken = default)
        => PostAsync("/analyze/similarity", taskId, documents, cancellationToken);

    public Task<IReadOnlyList<AlgoEvidence>> AnalyzePricingAsync(string taskId, IReadOnlyList<AlgoRawDocument> documents, CancellationToken cancellationToken = default)
        => PostAsync("/analyze/pricing", taskId, documents, cancellationToken);

    public Task<IReadOnlyList<AlgoEvidence>> AnalyzeMetadataAsync(string taskId, IReadOnlyList<AlgoRawDocument> documents, CancellationToken cancellationToken = default)
        => PostAsync("/analyze/metadata", taskId, documents, cancellationToken);

    private async Task<IReadOnlyList<AlgoEvidence>> PostAsync(
        string path, string taskId, IReadOnlyList<AlgoRawDocument> documents, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpCompareAlgoClient));

        var body = new JsonObject
        {
            ["taskId"] = taskId,
            ["documents"] = BuildDocuments(documents)
        };

        using var response = await TransientHttpRetry.ExecuteAsync(
            async ct => await client.PostAsJsonAsync(path.TrimStart('/'), body, JsonOptions, ct),
            _logger, $"算法服务 {path}", MaxAttempts, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await BuildServiceExceptionAsync(path, response, cancellationToken);
        }

        var payload = await response.Content.ReadFromJsonAsync<AnalyzeResponse>(JsonOptions, cancellationToken);
        return payload?.Evidences
            ?? throw new BusinessException(BidCompareErrorCodes.InvalidTaskState)
                .WithData("reason", $"算法服务 {path} 响应缺少 evidences");
    }

    /// <summary>非 2xx：读取 Python 端错误信封 {"code","message","details"} 透传，避免 EnsureSuccessStatusCode 丢诊断信息。</summary>
    private static async Task<BusinessException> BuildServiceExceptionAsync(
        string path, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        string? code = null;
        string? message = null;
        string? details = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("code", out var c)) code = c.GetString();
            if (document.RootElement.TryGetProperty("message", out var m)) message = m.GetString();
            if (document.RootElement.TryGetProperty("details", out var d)) details = d.ToString();
        }
        catch (JsonException)
        {
            // 非 JSON 错误体：原样摘要
        }
        var exception = new BusinessException(BidCompareErrorCodes.AlgoServiceFailed)
            .WithData("path", path)
            .WithData("statusCode", (int)response.StatusCode)
            .WithData("serviceCode", code ?? "")
            .WithData("message", message ?? "")
            .WithData("details", details ?? (body.Length <= 512 ? body : body[..512]));
        return exception;
    }

    /// <summary>jsonl 逐行解析为 blocks 数组，meta 原样透传（与 compare-algo AnalyzeRequest 契约一致）。</summary>
    private static JsonArray BuildDocuments(IReadOnlyList<AlgoRawDocument> documents)
    {
        var array = new JsonArray();
        foreach (var doc in documents)
        {
            var blocks = new JsonArray();
            foreach (var line in doc.GraphJsonl.Split(
                         '\n', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries))
            {
                if (JsonNode.Parse(line) is { } block)
                {
                    blocks.Add(block);
                }
            }

            array.Add(new JsonObject
            {
                ["docId"] = doc.DocId,
                ["blocks"] = blocks,
                ["meta"] = JsonNode.Parse(doc.MetaJson) ?? new JsonObject()
            });
        }
        return array;
    }

    private class AnalyzeResponse
    {
        public List<AlgoEvidence>? Evidences { get; set; }
    }
}
