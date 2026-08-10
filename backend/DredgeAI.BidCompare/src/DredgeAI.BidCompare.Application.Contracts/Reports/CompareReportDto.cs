using System;
using System.Collections.Generic;
using DredgeAI.BidCompare.Analysis;
using DredgeAI.BidCompare.Evidences;

namespace DredgeAI.BidCompare.Reports;

/// <summary>spec §6.1：CompareReport { taskId, summary, matrix, sections, generatedAt }。</summary>
public class CompareReportDto
{
    public Guid TaskId { get; set; }

    public ReportSummaryDto Summary { get; set; } = new();

    public SimilarityMatrixDto Matrix { get; set; } = new();

    /// <summary>固定三节：bidRiggingRisk（围标风险）/ clauseCompliance（条款响应）/ indicatorComparison（指标比选）。</summary>
    public List<ReportSectionDto> Sections { get; set; } = new();

    public DateTime GeneratedAt { get; set; }
}

public class ReportSummaryDto
{
    public int DocCount { get; set; }

    public int HighRiskCount { get; set; }

    public int MidRiskCount { get; set; }

    public int LowRiskCount { get; set; }

    /// <summary>spec §8-2：Top 5 最重要发现（按严重度+时间排序的标题）。</summary>
    public List<string> TopFindings { get; set; } = new();
}

public class ReportSectionDto
{
    public string Key { get; set; } = default!;

    public string Title { get; set; } = default!;

    /// <summary>证据与结果工作台同源（spec §8 一致性原则：同一证据 ID）。</summary>
    public List<EvidenceDto> Evidences { get; set; } = new();
}
