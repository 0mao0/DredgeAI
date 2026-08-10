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

    /// <summary>解析失败原因（spec §9 失败文档标注原因）。</summary>
    public string? ParseError { get; private set; }

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
    }

    public void MarkParsed(string irStorageKey, string? docMdStorageKey, int pageCount, double ocrLowConfidenceRatio)
    {
        ParseStatus = DocumentParseStatus.Parsed;
        ParseError = null;
        IrStorageKey = Check.NotNullOrWhiteSpace(irStorageKey, nameof(irStorageKey), maxLength: 512);
        DocMdStorageKey = docMdStorageKey;
        PageCount = pageCount;
        OcrLowConfidenceRatio = ocrLowConfidenceRatio;
    }

    public void MarkParseFailed(string error)
    {
        ParseStatus = DocumentParseStatus.Failed;
        ParseError = Check.NotNullOrWhiteSpace(error, nameof(error), maxLength: 2048);
    }
}
