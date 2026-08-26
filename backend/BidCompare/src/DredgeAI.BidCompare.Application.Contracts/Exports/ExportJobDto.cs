using System;

namespace DredgeAI.BidCompare.Exports;

/// <summary>导出任务句柄（spec §6.2）：POST 返回后立即轮询 GetExportJobAsync 直至 downloadUrl 非空。</summary>
public class ExportJobDto
{
    public Guid JobId { get; set; }

    public Guid TaskId { get; set; }

    public ExportFormat Format { get; set; }

    public ExportJobStatus Status { get; set; }

    public string? DownloadUrl { get; set; }

    public string? Error { get; set; }
}
