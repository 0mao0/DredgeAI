using System;
using System.Collections.Generic;
using DredgeAI.BidCompare.Clauses;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.BidCompare.CompareTasks;

public class CompareTaskDto : EntityDto<Guid>
{
    public string Name { get; set; } = default!;

    /// <summary>用户是否手动编辑过项目名（spec §3.3）。</summary>
    public bool NameEditedByUser { get; set; }

    /// <summary>解析完成后后端推断的项目名建议，未取到为 null。</summary>
    public string? SuggestedName { get; set; }

    public CompareTaskStatus Status { get; set; }

    public string? FailureReason { get; set; }

    public List<Guid> DocIds { get; set; } = new();

    public Guid? TenderDocId { get; set; }

    /// <summary>来源读标任务（P3）。</summary>
    public Guid? TenderReadingTaskId { get; set; }

    /// <summary>引用读标基准库版本（P3）。</summary>
    public int? TenderReadingBaselineVersion { get; set; }

    public List<ClauseDto>? ClauseSnapshot { get; set; }

    public CompareProgressDto Progress { get; set; } = new();

    public List<ComparePairDto>? Pairs { get; set; }

    public DateTime CreatedAt { get; set; }
}
