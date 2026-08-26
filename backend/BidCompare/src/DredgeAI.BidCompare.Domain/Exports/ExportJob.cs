using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.Exports;

/// <summary>导出任务句柄（spec §6.2 导出异步化）。</summary>
public class ExportJob : FullAuditedEntity<Guid>
{
    public Guid TaskId { get; private set; }

    public ExportFormat Format { get; private set; }

    public ExportJobStatus Status { get; private set; }

    /// <summary>导出文件对象存储 key：compare/{taskId}/exports/{jobId}.{pdf|docx}。</summary>
    public string? FileStorageKey { get; private set; }

    /// <summary>失败原因（spec §9 导出失败可重试）。</summary>
    public string? Error { get; private set; }

    protected ExportJob()
    {
    }

    public ExportJob(Guid id, Guid taskId, ExportFormat format) : base(id)
    {
        TaskId = taskId;
        Format = format;
        Status = ExportJobStatus.Pending;
    }

    public void MarkRunning()
    {
        Status = ExportJobStatus.Running;
        Error = null;
    }

    public void MarkSucceeded(string fileStorageKey)
    {
        Status = ExportJobStatus.Succeeded;
        FileStorageKey = Check.NotNullOrWhiteSpace(fileStorageKey, nameof(fileStorageKey), maxLength: 512);
        Error = null;
    }

    public void MarkFailed(string error)
    {
        Status = ExportJobStatus.Failed;
        Error = Check.NotNullOrWhiteSpace(error, nameof(error), maxLength: 2048);
    }
}
