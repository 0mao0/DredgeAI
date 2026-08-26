using System;
using DredgeAI.BidCompare.Documents;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.BidCompare.TenderReadings;

public class TenderReadingDocumentDto : EntityDto<Guid>
{
    public Guid TaskId { get; set; }

    public string FileName { get; set; } = default!;

    public long FileSize { get; set; }

    public DocumentParseStatus ParseStatus { get; set; }

    public string? ParseError { get; set; }

    public int? ParseProgress { get; set; }

    public string? ParseStage { get; set; }

    public string? ParseStageMessage { get; set; }

    public DateTime? ParseStartedAt { get; set; }

    public DateTime? ParseFinishedAt { get; set; }

    public int? PageCount { get; set; }

    public DateTime CreatedAt { get; set; }
}
