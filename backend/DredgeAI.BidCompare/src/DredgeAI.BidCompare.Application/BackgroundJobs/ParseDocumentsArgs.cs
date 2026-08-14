using System;
using System.Collections.Generic;

namespace DredgeAI.BidCompare.BackgroundJobs;

/// <summary>批量解析入参：一次任务并发提交多份文档给 AnGIneer。</summary>
public class ParseDocumentsArgs
{
    public Guid TaskId { get; set; }

    public List<Guid> DocumentIds { get; set; } = new();
}
