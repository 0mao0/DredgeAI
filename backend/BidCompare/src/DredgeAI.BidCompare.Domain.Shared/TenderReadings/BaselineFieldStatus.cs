namespace DredgeAI.BidCompare.TenderReadings;

/// <summary>基准库字段状态（比标模块只消费 Confirmed / Edited，或整体 Ready 的基准库）。</summary>
public enum BaselineFieldStatus : byte
{
    /// <summary>自动抽取，置信度高。</summary>
    Auto = 0,

    /// <summary>低置信度 / 规则与 LLM 冲突，等待人工。</summary>
    NeedsReview = 1,

    /// <summary>人工确认。</summary>
    Confirmed = 2,

    /// <summary>人工修改。</summary>
    Edited = 3
}
