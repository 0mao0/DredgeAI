using System;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.BidCompare.ClauseTemplates;

public class ClauseTemplateDto : AuditedEntityDto<Guid>
{
    public string Text { get; set; } = default!;

    public bool Mandatory { get; set; }

    public string? Category { get; set; }
}
