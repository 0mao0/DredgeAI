using System;
using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.MeetingBot;

/// <summary>
/// 晨会稿生成进度事件：首字到达前的管线关键节点（load/search/evidence/generate），
/// 流式端点以 SSE status 事件推给前端渲染「思考过程」，避免检索+等待首 token 阶段的黑屏干等。
/// </summary>
public sealed record SpeechDraftProgress(string Key, string Label);

/// <summary>晨会稿生成编排：检索知识库 → LLM 生成 → 落库并预热语音。</summary>
public interface ISpeechDraftStreamer
{
    /// <summary>非流式生成并落库，返回完整文本。</summary>
    Task<string> GenerateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式生成：LLM 增量文本经 onDelta 逐段透出（供 HttpApi 直写响应流），结束后落库；
    /// onProgress 在首字前推送管线进度事件（可为 null，如非流式回退路径）。
    /// </summary>
    Task<string> GenerateStreamAsync(
        Guid id,
        Func<string, CancellationToken, Task> onDelta,
        Func<SpeechDraftProgress, CancellationToken, Task>? onProgress = null,
        CancellationToken cancellationToken = default);
}
