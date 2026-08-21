namespace DredgeAI.BidCompare.TenderReadings;

/// <summary>基准库 8 类字段类别（P1 仅实现 ProjectInfo / CommercialData / ChapterOutline）。</summary>
public enum BaselineCategory : byte
{
    /// <summary>项目信息（名称 / 编号）。</summary>
    ProjectInfo = 0,

    /// <summary>废标 / 无效投标条款。</summary>
    RejectionClauses = 1,

    /// <summary>评分标准。</summary>
    EvaluationCriteria = 2,

    /// <summary>技术参数规格表。</summary>
    TechnicalParameters = 3,

    /// <summary>商务关键数据（限价 / 工期 / 质保期 / 付款方式）。</summary>
    CommercialData = 4,

    /// <summary>章节框架（目录脑图）。</summary>
    ChapterOutline = 5,

    /// <summary>签章规则。</summary>
    SealRules = 6,

    /// <summary>暗标格式规则。</summary>
    DarkBidFormatRules = 7
}
