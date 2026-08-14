using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
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
/// </summary>
public class HttpAnGineerClient : IAnGineerClient, ITransientDependency
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AnGineerOptions _options;

    public HttpAnGineerClient(IHttpClientFactory httpClientFactory, IOptions<AnGineerOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<string> SubmitAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
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

    public async Task<AnGineerJobState> GetStateAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var payload = await client.GetFromJsonAsync<StateResponse>($"/api/v1/documents/{jobId}/status", cancellationToken);
        return payload?.Status?.ToLowerInvariant() switch
        {
            "succeeded" or "completed" => AnGineerJobState.Succeeded,
            "failed" or "cancelled" => AnGineerJobState.Failed,
            _ => AnGineerJobState.Processing
        };
    }

    public async Task<AnGineerPackage> DownloadPackageAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var artifacts = await client.GetFromJsonAsync<ArtifactsResponse>(
            $"/api/v1/documents/{jobId}/artifacts", cancellationToken);
        if (artifacts?.Items == null)
        {
            throw new BusinessException(BidCompareErrorCodes.AnGineerParseFailed)
                .WithData("reason", "产物清单响应缺少 items");
        }

        var graphItem = artifacts.Items.FirstOrDefault(i => i.Name == "doc_blocks_graph.jsonl");
        var metaItem = artifacts.Items.FirstOrDefault(i => i.Name == "doc_blocks_graph_meta.json");
        if (graphItem?.Url == null || metaItem?.Url == null)
        {
            throw new BusinessException(BidCompareErrorCodes.AnGineerParseFailed)
                .WithData("reason", "产物包缺少 doc_blocks_graph.jsonl / doc_blocks_graph_meta.json");
        }

        var graphJsonl = await DownloadBytesAsync(client, graphItem.Url, cancellationToken);
        var metaJson = await DownloadBytesAsync(client, metaItem.Url, cancellationToken);

        // content.md / images 目前 AnGIneer v1 产物清单尚未开放（仅 graph/meta）；
        // 清单里一旦出现即随包下载，避免后续再改适配层。
        byte[]? contentMd = null;
        var contentMdItem = artifacts.Items.FirstOrDefault(i => i.Name == "content.md");
        if (contentMdItem?.Url != null)
        {
            contentMd = await DownloadBytesAsync(client, contentMdItem.Url, cancellationToken);
        }

        var images = new Dictionary<string, byte[]>();
        foreach (var imageItem in artifacts.Items.Where(i =>
                     i.Name != null && i.Name.StartsWith("images/", System.StringComparison.Ordinal) && i.Url != null))
        {
            images[imageItem.Name!] = await DownloadBytesAsync(client, imageItem.Url!, cancellationToken);
        }

        return new AnGineerPackage(graphJsonl, metaJson, ContentMd: contentMd, Images: images);
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpAnGineerClient));
        client.BaseAddress = new System.Uri(_options.BaseUrl.TrimEnd('/') + "/");
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            // AnGIneer v1 中间件要求 X-API-Key 头（Authorization: Bearer 不被识别）
            client.DefaultRequestHeaders.Add("X-API-Key", _options.ApiKey);
        }
        return client;
    }

    private static async Task<byte[]> DownloadBytesAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        await using var stream = await client.GetStreamAsync(url, cancellationToken);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
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
    }

    private class ArtifactsResponse
    {
        [JsonPropertyName("items")]
        public List<ArtifactItem>? Items { get; set; }
    }

    private class ArtifactItem
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
