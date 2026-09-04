using System;
using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.MeetingBot;

/// <summary>晨会稿生成编排：检索知识库 → LLM 生成 → 落库并预热语音。</summary>
public interface ISpeechDraftStreamer
{
    /// <summary>非流式生成并落库，返回完整文本。</summary>
    Task<string> GenerateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>流式生成：LLM 增量文本经 onDelta 逐段透出（供 HttpApi 直写响应流），结束后落库。</summary>
    Task<string> GenerateStreamAsync(
        Guid id,
        Func<string, CancellationToken, Task> onDelta,
        CancellationToken cancellationToken = default);
}
