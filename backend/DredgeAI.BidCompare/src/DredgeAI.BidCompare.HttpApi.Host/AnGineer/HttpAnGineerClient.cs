using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.AnGineer;

/// <summary>
/// AnGIneer HTTP API adapter（v1 documents API，docs-api 端口 8790）：
///   POST {BaseUrl}/api/v1/documents/parse  multipart 文件（stages=all：
///        source_prep→convert→raw_parse→popo→structure→fts→vectors→graph，
///        后续阶段可能改写 content.md 等产物，必须全量跑完再下载）→ { doc_id, task_id, status }
///   GET  {BaseUrl}/api/v1/documents/{docId}/status → { status: queued|processing|completed|failed|cancelled }
///   GET  {BaseUrl}/api/v1/documents/{docId}/artifacts → items[{ name, url }]
///   GET  {BaseUrl}/api/v1/documents/{docId}/artifacts/{name} → 产物文件字节
/// jobId 复用为 doc_id（状态与产物均按 doc_id 查询）。
/// 提交/下载带有限次指数退避重试；Timeout 由命名客户端注册处统一加长（200MB 级上传）。
/// </summary>
public class HttpAnGineerClient : IAnGineerClient, ITransientDependency
{
    private const int MaxAttempts = 3;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AnGineerOptions _options;
    private readonly ILogger<HttpAnGineerClient> _logger;

    public HttpAnGineerClient(
        IHttpClientFactory httpClientFactory,
        IOptions<AnGineerOptions> options,
        ILogger<HttpAnGineerClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 提交解析。openContent 为流工厂：每次重试重新打开底层文件流，
    /// 避免 StreamContent dispose（MultipartFormDataContent 释放时连带关闭）后重试复用已关闭流。
    /// </summary>
    public async Task<string> SubmitAsync(
        string fileName,
        Func<Task<Stream>> openContent,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var client = CreateClient();
                await using var content = await openContent();
                using var form = new MultipartFormDataContent();
                using var fileContent = new StreamContent(content);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                form.Add(fileContent, "file", fileName);

                using var response = await client.PostAsync(
                    "/api/v1/documents/parse?stages=all", form, cancellationToken);
                response.EnsureSuccessStatusCode();
                var payload = await response.Content.ReadFromJsonAsync<SubmitResponse>(cancellationToken: cancellationToken);
                return payload?.DocId
                    ?? throw new BusinessException(BidCompareErrorCodes.AnGineerParseFailed).WithData("reason", "提交响应缺少 doc_id");
            }
            catch (Exception ex) when (attempt < MaxAttempts
                                       && TransientHttpRetry.IsTransient(ex, cancellationToken))
            {
                var delay = TransientHttpRetry.Backoff(attempt);
                _logger.LogWarning(ex, "AnGIneer 提交解析瞬时失败（第 {Attempt}/{MaxAttempts} 次），{Delay}ms 后重试",
                    attempt, MaxAttempts, delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    public async Task<AnGineerJobStatus> GetStateAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var payload = await client.GetFromJsonAsync<StateResponse>($"/api/v1/documents/{jobId}/status", cancellationToken);
        var state = MapState(payload?.Status);
        return new AnGineerJobStatus(
            state,
            payload?.Progress ?? 0,
            payload?.Stage,
            payload?.StageMessage,
            payload?.Error);
    }

    public async Task<AnGineerJobStatus> ResumeAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        using var response = await client.PostAsync(
            $"/api/v1/documents/{jobId}/resume", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogInformation("AnGIneer 文档 {JobId} 正在解析中（resume 返回 409），按 Processing 继续轮询", jobId);
            return new AnGineerJobStatus(AnGineerJobState.Processing);
        }
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new BusinessException(BidCompareErrorCodes.AnGineerParseFailed)
                .WithData("reason", "AnGIneer API Key 无权恢复该文档（403）");
        }
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<StateResponse>(cancellationToken: cancellationToken);
        return new AnGineerJobStatus(
            MapState(payload?.Status),
            payload?.Progress ?? 0,
            payload?.Stage,
            payload?.StageMessage,
            payload?.Error);
    }

    public async Task<IReadOnlyList<AnGineerArtifact>> ListArtifactsAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var artifacts = await client.GetFromJsonAsync<ArtifactsResponse>(
            $"/api/v1/documents/{jobId}/artifacts", cancellationToken);
        if (artifacts?.Items == null)
        {
            throw new BusinessException(BidCompareErrorCodes.AnGineerParseFailed)
                .WithData("reason", "产物清单响应缺少 items");
        }
        return artifacts.Items
            .Where(i => i.Name != null && i.Url != null)
            .Select(i => new AnGineerArtifact(i.Name!, i.Url!))
            .ToList();
    }

    public async Task<IReadOnlyList<AnGineerHit>> SearchAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var request = new
        {
            query,
            top_k = topK,
            task_type = "content_qa",
            mode = "text"
        };
        using var response = await client.PostAsJsonAsync(
            "/api/knowledge/internal/retrieve", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "AnGIneer 知识检索失败（{Status}），返回空结果供上层降级",
                (int)response.StatusCode);
            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<RetrieveResponse>(
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
            cancellationToken);
        if (payload?.Items == null)
        {
            return [];
        }

        return payload.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.Text))
            .Select(i => new AnGineerHit(i.Text!, i.Title ?? "", i.Score, i.DocId ?? ""))
            .ToList();
    }

    /// <summary>流式打开产物（ResponseHeadersRead + 响应随流释放），带有限次退避重试。</summary>
    public async Task<Stream> OpenArtifactAsync(string jobId, AnGineerArtifact artifact, CancellationToken cancellationToken = default)
    {
        return await TransientHttpRetry.ExecuteAsync(
            async ct =>
            {
                var client = CreateClient();
                var response = await client.GetAsync(artifact.Url, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();
                var stream = await response.Content.ReadAsStreamAsync(ct);
                return (Stream)new OwnedStream(stream, response);
            },
            _logger, $"AnGIneer 下载产物 {artifact.Name}", MaxAttempts, cancellationToken);
    }

    /// <summary>未识别状态归一为 Processing（不中断轮询），但记录 warning 便于发现契约漂移。</summary>
    private AnGineerJobState UnknownToProcessing(string status)
    {
        _logger.LogWarning("AnGIneer 返回未知状态 \"{Status}\"，按 Processing 继续轮询", status);
        return AnGineerJobState.Processing;
    }

    private AnGineerJobState MapState(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "succeeded" or "completed" => AnGineerJobState.Succeeded,
            "failed" or "cancelled" => AnGineerJobState.Failed,
            "partial" => AnGineerJobState.Partial,
            "queued" or "processing" or null => AnGineerJobState.Processing,
            _ => UnknownToProcessing(status)
        };
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpAnGineerClient));
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            // AnGIneer v1 中间件要求 X-API-Key 头（Authorization: Bearer 不被识别）
            client.DefaultRequestHeaders.Add("X-API-Key", _options.ApiKey);
        }
        return client;
    }

    private class SubmitResponse
    {
        [JsonPropertyName("doc_id")]
        public string? DocId { get; set; }
    }

    private class StateResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("progress")]
        public int? Progress { get; set; }

        [JsonPropertyName("stage")]
        public string? Stage { get; set; }

        [JsonPropertyName("stage_message")]
        public string? StageMessage { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    private class ArtifactsResponse
    {
        [JsonPropertyName("items")]
        public List<ArtifactItem>? Items { get; set; }
    }

    private class RetrieveResponse
    {
        public List<RetrieveItem>? Items { get; set; }
    }

    private class RetrieveItem
    {
        public string? Text { get; set; }

        public string? Title { get; set; }

        public double Score { get; set; }

        [JsonPropertyName("doc_id")]
        public string? DocId { get; set; }
    }

    private class ArtifactItem
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
