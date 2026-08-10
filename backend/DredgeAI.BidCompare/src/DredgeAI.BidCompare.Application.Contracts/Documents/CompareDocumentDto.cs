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

    public int? PageCount { get; set; }

    public double? OcrLowConfidenceRatio { get; set; }

    public DateTime CreatedAt { get; set; }
}
