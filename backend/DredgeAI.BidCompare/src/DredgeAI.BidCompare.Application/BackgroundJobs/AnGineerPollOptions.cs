using System;

namespace DredgeAI.BidCompare.BackgroundJobs;

public class AnGineerPollOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>单次解析轮询总上限（MinerU/PoPo 等单步可达 15 分钟，整篇大标书可超 30 分钟）。</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(60);

    /// <summary>
    /// 停滞判定：processing 状态下 progress/stage/stageMessage 连续无变化的时长上限。
    /// 超时先 resume 一次，仍无进展则 fail-fast（默认 20 分钟，覆盖 MinerU/PoPo 单步 15 分钟的实测场景）。
    /// </summary>
    public TimeSpan StallTimeout { get; set; } = TimeSpan.FromMinutes(20);
}
