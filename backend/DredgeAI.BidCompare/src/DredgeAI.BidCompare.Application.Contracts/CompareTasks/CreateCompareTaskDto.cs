using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;
using DredgeAI.BidCompare.Clauses;

namespace DredgeAI.BidCompare.CompareTasks;

public class CreateCompareTaskDto
{
    [Required]
    [StringLength(128)]
    public string Name { get; set; } = default!;

    /// <summary>spec §6「创建任务（含条款清单快照）」：可选，提供即锁定快照。</summary>
    public List<ClauseInputDto>? Clauses { get; set; }

    /// <summary>上传会话 ID：提供时把会话中已上传文件转正为任务文档（不触发解析，解析仍由 startParse 控制）。</summary>
    public Guid? DraftId { get; set; }

    /// <summary>来源读标任务（P3）：提供时从读标基准库生成条款快照并锁定版本。</summary>
    public Guid? TenderReadingTaskId { get; set; }
}
