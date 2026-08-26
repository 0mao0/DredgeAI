using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.Documents;

public class CompareDocument : FullAuditedEntity<Guid>
{
    public Guid TaskId { get; private set; }

    public DocumentRole Role { get; private set; }

    public string FileName { get; private set; } = default!;

    public string FileExtension { get; private set; } = default!;

    public long FileSize { get; private set; }

    /// <summary>原始文件对象存储 key：compare/{taskId}/{docId}/origin.{ext}。</summary>
    public string OriginStorageKey { get; private set; } = default!;

    public DocumentParseStatus ParseStatus { get; private set; }

    /// <summary>AnGIneer 侧文档 id（POST /parse 返回的 doc_id；恢复解析复用，重解析不清空）。</summary>
    public string? AnGineerDocId { get; private set; }

    /// <summary>解析失败原因（spec §9 失败文档标注原因）。</summary>
    public string? ParseError { get; private set; }

    /// <summary>AnGIneer 解析进度（0~100，处理中为 AnGIneer progress，终态为 100）。</summary>
    public int? ParseProgress { get; private set; }

    /// <summary>AnGIneer 当前管线阶段（source_prep/convert/raw_parse/popo/structure/...）。</summary>
    public string? ParseStage { get; private set; }

    /// <summary>AnGIneer 当前阶段消息（如「MinerU 解析中」「阶段 structure 完成」）。</summary>
    public string? ParseStageMessage { get; private set; }

    /// <summary>本次解析开始时间（保留解析耗时用，前端按服务端时间戳计算）。</summary>
    public DateTime? ParseStartedAt { get; private set; }

    /// <summary>本次解析结束时间（成功/失败均记录）。</summary>
    public DateTime? ParseFinishedAt { get; private set; }

    /// <summary>内部适配 IR 对象存储 key：compare/{taskId}/{docId}/ir.json（由 AnGIneer doc_blocks_graph 按 v2 映射生成，非跨系统交付物）。</summary>
    public string? IrStorageKey { get; private set; }

    /// <summary>content.md 对象存储 key：compare/{taskId}/{docId}/content.md（AnGIneer 阅读流 Markdown，LLM 语义层用）。</summary>
    public string? DocMdStorageKey { get; private set; }

    public int? PageCount { get; private set; }

    /// <summary>OCR 低置信（source=ocr 且 confidence&lt;0.5）块占比，spec §4.5 概览用；source/confidence 缺失（v2 降级期）时记 0。</summary>
    public double? OcrLowConfidenceRatio { get; private set; }

    protected CompareDocument()
    {
    }

    public CompareDocument(
        Guid id,
        Guid taskId,
        DocumentRole role,
        string fileName,
        long fileSize,
        string originStorageKey) : base(id)
    {
        TaskId = taskId;
        Role = role;
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

    /// <summary>轮询时同步 AnGIneer 进度快照；终态时由调用方传入 100 与最终阶段。</summary>
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

    /// <summary>重新解析前复位：清空失败原因与旧产物引用。</summary>
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
        OcrLowConfidenceRatio = null;
    }

    public void MarkParsed(string irStorageKey, string? docMdStorageKey, int pageCount, double ocrLowConfidenceRatio)
    {
        ParseStatus = DocumentParseStatus.Parsed;
        ParseError = null;
        IrStorageKey = Check.NotNullOrWhiteSpace(irStorageKey, nameof(irStorageKey), maxLength: 512);
        DocMdStorageKey = docMdStorageKey;
        PageCount = pageCount;
        OcrLowConfidenceRatio = ocrLowConfidenceRatio;
        ParseProgress = 100;
        ParseFinishedAt ??= DateTime.UtcNow;
    }

    public void MarkParseFailed(string error)
    {
        ParseStatus = DocumentParseStatus.Failed;
        var value = Check.NotNullOrWhiteSpace(error, nameof(error));
        ParseError = value.Length <= 2048 ? value : value[..2048];
        ParseProgress = 100;
        ParseFinishedAt ??= DateTime.UtcNow;
    }
}
