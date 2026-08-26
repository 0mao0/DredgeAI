using System;
using DredgeAI.BidCompare.Documents;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.TenderReadings;

/// <summary>读标任务关联文档（招标文件上传与解析记录）。</summary>
public class TenderReadingDocument : FullAuditedEntity<Guid>
{
    public Guid TaskId { get; private set; }

    public string FileName { get; private set; } = default!;

    public string FileExtension { get; private set; } = default!;

    public long FileSize { get; private set; }

    public string OriginStorageKey { get; private set; } = default!;

    public DocumentParseStatus ParseStatus { get; private set; }

    public string? AnGineerDocId { get; private set; }

    public string? IrStorageKey { get; private set; }

    public string? DocMdStorageKey { get; private set; }

    public string? ParseError { get; private set; }

    public int? ParseProgress { get; private set; }

    public string? ParseStage { get; private set; }

    public string? ParseStageMessage { get; private set; }

    public DateTime? ParseStartedAt { get; private set; }

    public DateTime? ParseFinishedAt { get; private set; }

    public int? PageCount { get; private set; }

    protected TenderReadingDocument()
    {
    }

    public TenderReadingDocument(
        Guid id,
        Guid taskId,
        string fileName,
        long fileSize,
        string originStorageKey) : base(id)
    {
        TaskId = taskId;
        FileName = Check.NotNullOrWhiteSpace(fileName, nameof(fileName), maxLength: 256);
        FileExtension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        FileSize = fileSize;
        OriginStorageKey = Check.NotNullOrWhiteSpace(originStorageKey, nameof(originStorageKey), maxLength: 512);
        ParseStatus = DocumentParseStatus.Pending;
    }

    public void MarkParsing()
    {
        ParseStatus = DocumentParseStatus.Parsing;
        ParseError = null;
        ParseProgress = 0;
        ParseStage = null;
        ParseStageMessage = null;
        ParseStartedAt = DateTime.UtcNow;
        ParseFinishedAt = null;
    }

    public void SetAnGineerDocId(string? anGineerDocId)
    {
        var value = anGineerDocId?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        AnGineerDocId = value!.Length <= 128 ? value : value![..128];
    }

    public void UpdateParseProgress(int progress, string? stage, string? stageMessage)
    {
        if (ParseStatus != DocumentParseStatus.Parsing)
        {
            return;
        }

        ParseProgress = Math.Clamp(progress, 0, 100);
        ParseStage = stage;
        ParseStageMessage = stageMessage;
    }

    public void MarkPendingForReparse()
    {
        ParseStatus = DocumentParseStatus.Pending;
        ParseError = null;
        ParseProgress = null;
        ParseStage = null;
        ParseStageMessage = null;
        ParseStartedAt = null;
        ParseFinishedAt = null;
        IrStorageKey = null;
        DocMdStorageKey = null;
        PageCount = null;
    }

    public void MarkParsed(string irStorageKey, string? docMdStorageKey, int pageCount)
    {
        ParseStatus = DocumentParseStatus.Parsed;
        ParseError = null;
        IrStorageKey = Check.NotNullOrWhiteSpace(irStorageKey, nameof(irStorageKey), maxLength: 512);
        DocMdStorageKey = docMdStorageKey;
        PageCount = pageCount;
        ParseProgress = 100;
        ParseStage = "completed";
        ParseStageMessage = "解析完成";
        ParseFinishedAt ??= DateTime.UtcNow;
    }

    public void MarkParseFailed(string error)
    {
        ParseStatus = DocumentParseStatus.Failed;
        var value = Check.NotNullOrWhiteSpace(error, nameof(error));
        ParseError = value.Length <= 2048 ? value : value[..2048];
        ParseProgress = 100;
        ParseStage = "failed";
        ParseStageMessage = error;
        ParseFinishedAt ??= DateTime.UtcNow;
    }
}
