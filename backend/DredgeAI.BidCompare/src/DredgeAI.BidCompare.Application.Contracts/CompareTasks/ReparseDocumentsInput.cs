using System;
using System.Collections.Generic;

namespace DredgeAI.BidCompare.CompareTasks;

/// <summary>重新解析失败文档入参：docIds 缺省时解析全部失败文档。</summary>
public class ReparseDocumentsInput
{
    public List<Guid>? DocIds { get; set; }
}
