using System;
using System.Collections.Generic;

namespace DredgeAI.BidCompare.BackgroundJobs;

public class CompareDocumentsArgs
{
    public Guid TaskId { get; set; }

    /// <summary>重跑指定比对对（缺省为全量重跑）。</summary>
    public List<Guid>? PairIds { get; set; }
}
