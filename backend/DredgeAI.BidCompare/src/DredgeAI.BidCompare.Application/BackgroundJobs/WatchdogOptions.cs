using System;

namespace DredgeAI.BidCompare.BackgroundJobs;

/// <summary>卡死看门狗配置（宿主 Watchdog 配置节绑定）。</summary>
public class WatchdogOptions
{
    /// <summary>是否启用巡检（默认开，生产不应关闭）。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>巡检周期，默认 1 分钟。</summary>
    public TimeSpan Period { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>文档 Parsing 超时（默认 65 分钟 = AnGIneer 轮询总上限 60 分钟 + 5 分钟宽限；必须大于 AnGIneer:Timeout，否则原 Job 仍在正常轮询时会重复入队恢复 Job）。</summary>
    public TimeSpan DocumentParsingTimeout { get; set; } = TimeSpan.FromMinutes(65);

    /// <summary>任务 Comparing/Analyzing 超时（默认 90 分钟：覆盖算法端点超时+重试（约 30 分钟）与 AI 分析最坏耗时（8 份标书串行判定约 48 分钟）并留有余量；Job 心跳会刷新 LastModificationTime，长任务正常推进时不会误触）。</summary>
    public TimeSpan TaskTimeout { get; set; } = TimeSpan.FromMinutes(90);

    /// <summary>读标任务解析完成（Parsed）但抽取未启动的恢复阈值（默认 5 分钟：抽取正常在解析落定后数秒内入队，超过即视为入队失败/Job 崩溃，看门狗自动补拉抽取任务）。</summary>
    public TimeSpan TenderReadExtractRecoveryInterval { get; set; } = TimeSpan.FromMinutes(5);
}
