using System;
using System.Collections.Generic;
using DredgeAI.BidCompare.Clauses;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.BidCompare.CompareTasks;

public class CompareTaskDto : EntityDto<Guid>
{
    public string Name { get; set; } = default!;

    public CompareTaskStatus Status { get; set; }

    public List<Guid> DocIds { get; set; } = new();

    public Guid? TenderDocId { get; set; }

    public List<ClauseDto>? ClauseSnapshot { get; set; }

    public CompareProgressDto Progress { get; set; } = new();

    public DateTime CreatedAt { get; set; }
}
