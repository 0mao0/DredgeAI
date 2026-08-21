using System;

namespace DredgeAI.BidCompare.BackgroundJobs;

/// <summary>孤儿数据清扫配置（宿主 Cleanup 配置节绑定）。</summary>
public class CleanupOptions
{
    /// <summary>是否启用清扫（默认开）。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>清扫周期，默认 1 小时。</summary>
    public TimeSpan Period { get; set; } = TimeSpan.FromHours(1);

    /// <summary>上传会话（草稿）保留时长：超时未转正的草稿文档与存储对象删除，默认 24 小时。</summary>
    public TimeSpan DraftRetention { get; set; } = TimeSpan.FromHours(24);

    /// <summary>导出文件保留时长：超时删除导出对象与任务句柄行（报告可按需重新生成），默认 7 天。</summary>
    public TimeSpan ExportRetention { get; set; } = TimeSpan.FromDays(7);
}
