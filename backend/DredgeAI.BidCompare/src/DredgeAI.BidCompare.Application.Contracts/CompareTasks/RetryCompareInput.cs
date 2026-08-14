using System;
using System.Collections.Generic;

namespace DredgeAI.BidCompare.CompareTasks;

/// <summary>重新对比入参：pairIds 缺省时重跑全部比对对。</summary>
public class RetryCompareInput
{
    public List<Guid>? PairIds { get; set; }
}
