using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.Analysis;

/// <summary>算法服务 HTTP client：POST {BaseUrl}/analyze/similarity|pricing|metadata。</summary>
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

    public Task<IReadOnlyList<AlgoEvidence>> AnalyzeSimilarityAsync(IReadOnlyList<AlgoIrDocument> documents, CancellationToken cancellationToken = default)
        => PostAsync("/analyze/similarity", documents, cancellationToken);

    public Task<IReadOnlyList<AlgoEvidence>> AnalyzePricingAsync(IReadOnlyList<AlgoIrDocument> documents, CancellationToken cancellationToken = default)
        => PostAsync("/analyze/pricing", documents, cancellationToken);

    public Task<IReadOnlyList<AlgoEvidence>> AnalyzeMetadataAsync(IReadOnlyList<AlgoIrDocument> documents, CancellationToken cancellationToken = default)
        => PostAsync("/analyze/metadata", documents, cancellationToken);

    private async Task<IReadOnlyList<AlgoEvidence>> PostAsync(
        string path, IReadOnlyList<AlgoIrDocument> documents, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpCompareAlgoClient));
        client.BaseAddress = new System.Uri(_options.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = System.TimeSpan.FromSeconds(_options.TimeoutSeconds);

        using var response = await client.PostAsJsonAsync(
            path.TrimStart('/'),
            new { documents },
            JsonOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AnalyzeResponse>(JsonOptions, cancellationToken);
        return payload?.Evidences
            ?? throw new BusinessException(BidCompareErrorCodes.InvalidTaskState)
                .WithData("reason", $"算法服务 {path} 响应缺少 evidences");
    }

    private class AnalyzeResponse
    {
        public List<AlgoEvidence>? Evidences { get; set; }
    }
}
