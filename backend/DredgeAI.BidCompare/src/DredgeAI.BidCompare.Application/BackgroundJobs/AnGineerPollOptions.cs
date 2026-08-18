using System;

namespace DredgeAI.BidCompare.BackgroundJobs;

public class AnGineerPollOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 停滞判定：processing 状态下 progress/stage/stageMessage 连续无变化的时长上限。
    /// 超时先 resume 一次，仍无进展则 fail-fast（默认 3 分钟，远小于轮询总超时 30 分钟）。
    /// </summary>
    public TimeSpan StallTimeout { get; set; } = TimeSpan.FromMinutes(3);
}
