using System;
using DredgeAI.BidCompare.Documents;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.BidCompare.Drafts;

public class CompareDraftDocumentDto : EntityDto<Guid>
{
    public Guid DraftId { get; set; }

    public DocumentRole Role { get; set; }

    public string FileName { get; set; } = default!;

    public long FileSize { get; set; }

    public DateTime CreatedAt { get; set; }
}
