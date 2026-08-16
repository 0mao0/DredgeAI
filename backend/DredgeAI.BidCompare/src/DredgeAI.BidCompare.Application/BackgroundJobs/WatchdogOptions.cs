using System;

namespace DredgeAI.BidCompare.BackgroundJobs;

/// <summary>卡死看门狗配置（宿主 Watchdog 配置节绑定）。</summary>
public class WatchdogOptions
{
    /// <summary>是否启用巡检（默认开，生产不应关闭）。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>巡检周期，默认 1 分钟。</summary>
    public TimeSpan Period { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>文档 Parsing 超时（默认 35 分钟 = AnGIneer 轮询 30 分钟 + 5 分钟宽限）。</summary>
    public TimeSpan DocumentParsingTimeout { get; set; } = TimeSpan.FromMinutes(35);

    /// <summary>任务 Comparing/Analyzing 超时（默认 40 分钟，覆盖算法端点超时 + 重试 + AI 分析）。</summary>
    public TimeSpan TaskTimeout { get; set; } = TimeSpan.FromMinutes(40);
}
