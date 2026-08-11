using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.Analysis;

/// <summary>算法服务 HTTP client：POST {BaseUrl}/analyze/similarity|pricing|metadata，请求体为 AnGIneer 原始产物。</summary>
public class HttpCompareAlgoClient : ICompareAlgoClient, ITransientDependency
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AlgoServiceOptions _options;

    public HttpCompareAlgoClient(IHttpClientFactory httpClientFactory, IOptions<AlgoServiceOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
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
        client.BaseAddress = new System.Uri(_options.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = System.TimeSpan.FromSeconds(_options.TimeoutSeconds);

        var body = new JsonObject
        {
            ["taskId"] = taskId,
            ["documents"] = BuildDocuments(documents)
        };

        using var response = await client.PostAsJsonAsync(
            path.TrimStart('/'),
            body,
            JsonOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AnalyzeResponse>(JsonOptions, cancellationToken);
        return payload?.Evidences
            ?? throw new BusinessException(BidCompareErrorCodes.InvalidTaskState)
                .WithData("reason", $"算法服务 {path} 响应缺少 evidences");
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
