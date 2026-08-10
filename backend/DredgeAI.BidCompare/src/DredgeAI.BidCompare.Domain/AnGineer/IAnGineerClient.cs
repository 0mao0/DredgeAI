using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.AnGineer;

public enum AnGineerJobState
{
    Processing = 0,
    Succeeded = 1,
    Failed = 2
}

/// <summary>AnGIneer 解析产物包（v2 §1 数据源：doc_blocks_graph.jsonl + doc_blocks_graph_meta.json + content.md + images/）。</summary>
public record AnGineerPackage(
    byte[] GraphJsonl,
    byte[] MetaJson,
    byte[]? ContentMd,
    IReadOnlyDictionary<string, byte[]> Images);

/// <summary>
/// AnGIneer 解析流水线 adapter（提交文档 → 轮询 → 下载产物包）。
/// 提供方部署形态变化只改实现，契约不变（spec §2 非目标：不约束提供方内部流水线）。
/// </summary>
public interface IAnGineerClient
{
    /// <summary>提交解析任务，返回提供方任务 id。</summary>
    Task<string> SubmitAsync(string fileName, Stream content, CancellationToken cancellationToken = default);

    Task<AnGineerJobState> GetStateAsync(string jobId, CancellationToken cancellationToken = default);

    Task<AnGineerPackage> DownloadPackageAsync(string jobId, CancellationToken cancellationToken = default);
}
