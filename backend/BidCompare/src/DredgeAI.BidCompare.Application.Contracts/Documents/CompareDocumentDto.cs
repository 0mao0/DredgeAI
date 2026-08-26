using System;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.BidCompare.Documents;

public class CompareDocumentDto : EntityDto<Guid>
{
    public Guid TaskId { get; set; }

    public DocumentRole Role { get; set; }

    public string FileName { get; set; } = default!;

    public long FileSize { get; set; }

    public DocumentParseStatus ParseStatus { get; set; }

    public string? ParseError { get; set; }

    /// <summary>AnGIneer 解析进度（0~100）。</summary>
    public int? ParseProgress { get; set; }

    /// <summary>AnGIneer 当前管线阶段。</summary>
    public string? ParseStage { get; set; }

    /// <summary>AnGIneer 当前阶段消息。</summary>
    public string? ParseStageMessage { get; set; }

    /// <summary>本次解析开始时间。</summary>
    public DateTime? ParseStartedAt { get; set; }

    /// <summary>本次解析结束时间。</summary>
    public DateTime? ParseFinishedAt { get; set; }

    public int? PageCount { get; set; }

    public double? OcrLowConfidenceRatio { get; set; }

    public DateTime CreatedAt { get; set; }
}
