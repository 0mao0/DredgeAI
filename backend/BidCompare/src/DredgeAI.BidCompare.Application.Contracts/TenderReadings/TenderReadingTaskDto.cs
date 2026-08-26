using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.BidCompare.TenderReadings;

public class TenderReadingTaskDto : EntityDto<Guid>
{
    public string Name { get; set; } = default!;

    public string? ProjectCode { get; set; }

    public TenderReadingTaskStatus Status { get; set; }

    public string ProgressStage { get; set; } = default!;

    public int ProgressPercent { get; set; }

    public string? FailureReason { get; set; }

    public List<Guid> DocIds { get; set; } = new();

    public DateTime CreatedAt { get; set; }
}
