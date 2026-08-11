using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Analysis;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Evidences;
using DredgeAI.BidCompare.Reports;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace DredgeAI.BidCompare.Reporting;

/// <summary>
/// 报告 JSON 组装（spec §8）：摘要（高/中/低计数 + Top5）、相似度矩阵、
/// 三节证据（围标风险 = similarity/pricing/metadata；条款响应 = clause；指标比选 = indicator）。
/// 证据与结果工作台同源（同一 EvidenceItem）。
/// </summary>
public class ReportBuilder : ITransientDependency
{
    private readonly IRepository<EvidenceItem, Guid> _evidenceRepository;
    private readonly IRepository<CompareDocument, Guid> _documentRepository;
    private readonly IClock _clock;

    public ReportBuilder(
        IRepository<EvidenceItem, Guid> evidenceRepository,
        IRepository<CompareDocument, Guid> documentRepository,
        IClock clock)
    {
        _evidenceRepository = evidenceRepository;
        _documentRepository = documentRepository;
        _clock = clock;
    }

    public async Task<CompareReportDto> BuildAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var evidences = (await _evidenceRepository.GetListAsync(e => e.TaskId == taskId))
            .Select(EvidenceMapper.ToDto)
            .OrderBy(e => e.Severity)
            .ThenBy(e => e.Title)
            .ToList();

        var docCount = await _documentRepository.CountAsync(d =>
            d.TaskId == taskId && d.Role == DocumentRole.Bid && d.ParseStatus == DocumentParseStatus.Parsed);

        return new CompareReportDto
        {
            TaskId = taskId,
            GeneratedAt = _clock.Now,
            Summary = new ReportSummaryDto
            {
                DocCount = docCount,
                HighRiskCount = evidences.Count(e => e.Severity == EvidenceSeverity.High),
                MidRiskCount = evidences.Count(e => e.Severity == EvidenceSeverity.Mid),
                LowRiskCount = evidences.Count(e => e.Severity == EvidenceSeverity.Low),
                TopFindings = evidences.Take(5).Select(e => e.Title).ToList()
            },
            Matrix = await BuildMatrixAsync(taskId, evidences),
            Sections = new List<ReportSectionDto>
            {
                new()
                {
                    Key = "bidRiggingRisk",
                    Title = "围标风险",
                    Evidences = evidences.Where(e =>
                        e.Type is EvidenceType.Similarity or EvidenceType.Pricing or EvidenceType.Metadata).ToList()
                },
                new()
                {
                    Key = "clauseCompliance",
                    Title = "强制性条款响应",
                    Evidences = evidences.Where(e => e.Type == EvidenceType.Clause).ToList()
                },
                new()
                {
                    Key = "indicatorComparison",
                    Title = "关键指标比选",
                    Evidences = evidences.Where(e => e.Type == EvidenceType.Indicator).ToList()
                }
            }
        };
    }

    private async Task<SimilarityMatrixDto> BuildMatrixAsync(Guid taskId, List<EvidenceDto> evidences)
    {
        var docs = (await _documentRepository.GetListAsync(d =>
                d.TaskId == taskId && d.Role == DocumentRole.Bid && d.ParseStatus == DocumentParseStatus.Parsed))
            .OrderBy(d => d.CreationTime)
            .ToList();

        var similarityEvidences = evidences.Where(e => e.Type == EvidenceType.Similarity).ToList();
        var cells = new List<SimilarityMatrixCellDto>();
        foreach (var a in docs)
        {
            foreach (var b in docs)
            {
                var similarity = a.Id == b.Id
                    ? 1.0
                    : similarityEvidences
                        .Where(e => e.Metrics?.Similarity != null && e.DocIds.Contains(a.Id) && e.DocIds.Contains(b.Id))
                        .Select(e => e.Metrics!.Similarity!.Value)
                        .DefaultIfEmpty(0.0)
                        .Max();
                cells.Add(new SimilarityMatrixCellDto
                {
                    DocAId = a.Id,
                    DocBId = b.Id,
                    Similarity = Math.Round(similarity, 4)
                });
            }
        }

        return new SimilarityMatrixDto
        {
            DocIds = docs.Select(d => d.Id).ToList(),
            Cells = cells
        };
    }
}
