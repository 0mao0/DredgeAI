using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.AnGineer;

public enum AnGineerJobState
{
    Processing = 0,
    Succeeded = 1,
    Failed = 2,
    Partial = 3
}

/// <summary>AnGIneer /status 接口的完整轮询快照：状态 + 总体进度 + 当前阶段 + 阶段消息。</summary>
public record AnGineerJobStatus(
    AnGineerJobState State,
    int Progress = 0,
    string? Stage = null,
    string? StageMessage = null,
    string? Error = null);

/// <summary>AnGIneer 产物清单项（name + 下载 url）。</summary>
public record AnGineerArtifact(string Name, string Url);

/// <summary>AnGIneer 解析产物包（v2 §1 数据源：doc_blocks_graph.jsonl + doc_blocks_graph_meta.json + content.md + images/）。</summary>
public record AnGineerPackage(
    byte[] GraphJsonl,
    byte[] MetaJson,
    byte[]? ContentMd,
    IReadOnlyDictionary<string, byte[]> Images);

/// <summary>
/// AnGIneer 解析流水线 adapter（提交文档 → 轮询 → 产物清单 → 逐个产物流式下载）。
/// 提供方部署形态变化只改实现，契约不变（spec §2 非目标：不约束提供方内部流水线）。
/// </summary>
public interface IAnGineerClient
{
    /// <summary>
    /// 提交解析任务，返回提供方任务 id。
    /// 传入流工厂而非流实例：内部重试时每次重新打开，避免 StreamContent dispose 后复用已关闭流
    /// （Cannot access a closed file）。
    /// </summary>
    Task<string> SubmitAsync(string fileName, Func<Task<Stream>> openContent, CancellationToken cancellationToken = default);

    Task<AnGineerJobStatus> GetStateAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>恢复已存在 doc_id 的解析任务（POST /api/v1/documents/{docId}/resume）；409 视为 Processing。</summary>
    Task<AnGineerJobStatus> ResumeAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>列出解析产物清单（doc_blocks_graph.jsonl / doc_blocks_graph_meta.json / content.md / images/...）。</summary>
    Task<IReadOnlyList<AnGineerArtifact>> ListArtifactsAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>流式打开单个产物（调用方负责 Dispose 返回的流），大产物不整份驻留内存。</summary>
    Task<Stream> OpenArtifactAsync(string jobId, AnGineerArtifact artifact, CancellationToken cancellationToken = default);
}
