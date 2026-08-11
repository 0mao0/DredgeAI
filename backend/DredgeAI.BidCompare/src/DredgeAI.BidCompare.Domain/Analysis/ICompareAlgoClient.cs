using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.Analysis;

/// <summary>发送给算法服务的单份 AnGIneer 原始产物（docId 为本系统文档 Guid 字符串；GraphJsonl 为 doc_blocks_graph.jsonl 原文；MetaJson 为 doc_blocks_graph_meta.json 原文）。</summary>
public record AlgoRawDocument(string DocId, string GraphJsonl, string MetaJson);

/// <summary>
/// 算法服务返回的证据项（spec §6.1 Evidence 子集，aiGenerated 恒为 false 由本服务补充）。
/// JSON 字段名逐字遵守：type/severity/docIds/locations/docId/blockIds/metrics/title/description。
/// </summary>
public class AlgoEvidence
{
    public string Type { get; set; } = default!;

    public string Severity { get; set; } = default!;

    public List<string> DocIds { get; set; } = new();

    public List<AlgoEvidenceLocation> Locations { get; set; } = new();

    public Dictionary<string, JsonElement>? Metrics { get; set; }

    public string Title { get; set; } = default!;

    public string Description { get; set; } = default!;
}

public class AlgoEvidenceLocation
{
    public string DocId { get; set; } = default!;

    public List<string> BlockIds { get; set; } = new();
}

/// <summary>
/// Python 算法服务 client（spec §3.1 compare-algo：纯确定性，输入 AnGIneer 原始产物，输出结构化证据项）。
/// 三个端点：POST /analyze/similarity、/analyze/pricing、/analyze/metadata。
/// </summary>
public interface ICompareAlgoClient
{
    Task<IReadOnlyList<AlgoEvidence>> AnalyzeSimilarityAsync(string taskId, IReadOnlyList<AlgoRawDocument> documents, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlgoEvidence>> AnalyzePricingAsync(string taskId, IReadOnlyList<AlgoRawDocument> documents, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlgoEvidence>> AnalyzeMetadataAsync(string taskId, IReadOnlyList<AlgoRawDocument> documents, CancellationToken cancellationToken = default);
}
