using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.AnGineer;

/// <summary>
/// AnGIneer HTTP API adapter。约定提供方接口形态：
///   POST {BaseUrl}/api/parse          multipart 文件 → { "jobId": "..." }
///   GET  {BaseUrl}/api/parse/{jobId}  → { "state": "processing|succeeded|failed" }
///   GET  {BaseUrl}/api/parse/{jobId}/package → zip（doc_blocks_graph.jsonl + doc_blocks_graph_meta.json + content.md + images/）
/// 形态变化（如改消息队列）只替换本类（spec §11 待决事项1）。
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

        using var response = await client.PostAsync("/api/parse", form, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<SubmitResponse>(cancellationToken: cancellationToken);
        return payload?.JobId
            ?? throw new BusinessException(BidCompareErrorCodes.AnGineerParseFailed).WithData("reason", "提交响应缺少 jobId");
    }

    public async Task<AnGineerJobState> GetStateAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var payload = await client.GetFromJsonAsync<StateResponse>($"/api/parse/{jobId}", cancellationToken);
        return payload?.State?.ToLowerInvariant() switch
        {
            "succeeded" => AnGineerJobState.Succeeded,
            "failed" => AnGineerJobState.Failed,
            _ => AnGineerJobState.Processing
        };
    }

    public async Task<AnGineerPackage> DownloadPackageAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        await using var zipStream = await client.GetStreamAsync($"/api/parse/{jobId}/package", cancellationToken);
        using var buffer = new MemoryStream();
        await zipStream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        byte[]? graphJsonl = null;
        byte[]? metaJson = null;
        byte[]? contentMd = null;
        var images = new Dictionary<string, byte[]>();

        using var archive = new ZipArchive(buffer, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            using var entryStream = entry.Open();
            using var entryBuffer = new MemoryStream();
            await entryStream.CopyToAsync(entryBuffer, cancellationToken);
            if (name.EndsWith("doc_blocks_graph.jsonl", System.StringComparison.OrdinalIgnoreCase))
            {
                graphJsonl = entryBuffer.ToArray();
            }
            else if (name.EndsWith("doc_blocks_graph_meta.json", System.StringComparison.OrdinalIgnoreCase))
            {
                metaJson = entryBuffer.ToArray();
            }
            else if (name.EndsWith("content.md", System.StringComparison.OrdinalIgnoreCase))
            {
                contentMd = entryBuffer.ToArray();
            }
            else if (name.StartsWith("images/", System.StringComparison.OrdinalIgnoreCase) && entry.Length > 0)
            {
                images[name] = entryBuffer.ToArray();
            }
        }

        if (graphJsonl == null || metaJson == null)
        {
            throw new BusinessException(BidCompareErrorCodes.AnGineerParseFailed)
                .WithData("reason", "产物包缺少 doc_blocks_graph.jsonl / doc_blocks_graph_meta.json");
        }
        return new AnGineerPackage(graphJsonl, metaJson, contentMd, images);
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpAnGineerClient));
        client.BaseAddress = new System.Uri(_options.BaseUrl.TrimEnd('/') + "/");
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
        return client;
    }

    private class SubmitResponse
    {
        public string? JobId { get; set; }
    }

    private class StateResponse
    {
        public string? State { get; set; }
    }
}
