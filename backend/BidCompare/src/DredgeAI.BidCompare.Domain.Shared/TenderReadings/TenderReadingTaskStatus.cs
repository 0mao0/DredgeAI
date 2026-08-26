namespace DredgeAI.BidCompare.TenderReadings;

/// <summary>读标任务状态机（P1 基础链路：上传 → 解析 → 抽取 → Ready/Partial/Failed）。</summary>
public enum TenderReadingTaskStatus : byte
{
    /// <summary>文件上传中。</summary>
    Uploading = 0,

    /// <summary>AnGIneer 解析中。</summary>
    Parsing = 1,

    /// <summary>已生成内部 IR，等待抽取。</summary>
    Parsed = 2,

    /// <summary>规则/LLM 抽取中。</summary>
    Extracting = 3,

    /// <summary>存在低置信度字段，等待人工复核。</summary>
    Reviewing = 4,

    /// <summary>基准库可被比标模块消费。</summary>
    Ready = 5,

    /// <summary>部分字段失败，可查看可重试。</summary>
    Partial = 6,

    /// <summary>解析或抽取整体失败。</summary>
    Failed = 7
}
