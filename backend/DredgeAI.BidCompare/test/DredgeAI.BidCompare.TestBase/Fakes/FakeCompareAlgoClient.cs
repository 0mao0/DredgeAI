using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.Analysis;

/// <summary>可编程 Fake：按端点预设响应；FailWith 非空时全部抛 HttpRequestException 模拟服务不可用。</summary>
public class FakeCompareAlgoClient : ICompareAlgoClient
{
    public List<AlgoEvidence> SimilarityEvidences { get; set; } = new();

    public List<AlgoEvidence> PricingEvidences { get; set; } = new();

    public List<AlgoEvidence> MetadataEvidences { get; set; } = new();

    public string? FailWith { get; set; }

    public IReadOnlyList<AlgoIrDocument>? LastRequest { get; private set; }

    public Task<IReadOnlyList<AlgoEvidence>> AnalyzeSimilarityAsync(IReadOnlyList<AlgoIrDocument> documents, CancellationToken cancellationToken = default)
    {
        LastRequest = documents;
        return Respond(SimilarityEvidences);
    }

    public Task<IReadOnlyList<AlgoEvidence>> AnalyzePricingAsync(IReadOnlyList<AlgoIrDocument> documents, CancellationToken cancellationToken = default)
    {
        LastRequest = documents;
        return Respond(PricingEvidences);
    }

    public Task<IReadOnlyList<AlgoEvidence>> AnalyzeMetadataAsync(IReadOnlyList<AlgoIrDocument> documents, CancellationToken cancellationToken = default)
    {
        LastRequest = documents;
        return Respond(MetadataEvidences);
    }

    private Task<IReadOnlyList<AlgoEvidence>> Respond(List<AlgoEvidence> evidences)
    {
        if (FailWith != null)
        {
            throw new System.Net.Http.HttpRequestException(FailWith);
        }
        return Task.FromResult<IReadOnlyList<AlgoEvidence>>(evidences);
    }
}
