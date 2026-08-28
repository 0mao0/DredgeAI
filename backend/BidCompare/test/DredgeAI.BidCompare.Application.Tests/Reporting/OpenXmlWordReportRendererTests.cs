using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DredgeAI.BidCompare.Analysis;
using DredgeAI.BidCompare.Evidences;
using DredgeAI.BidCompare.Reports;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace DredgeAI.BidCompare.Reporting;

public class OpenXmlWordReportRendererTests
{
    [Fact]
    public async Task Render_Should_Produce_Valid_Docx_With_Tokens_Replaced()
    {
        var templatePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "template.docx");
        var renderer = new OpenXmlWordReportRenderer(
            Options.Create(new ReportExportOptions { TemplatePath = templatePath }));

        var docA = Guid.NewGuid();
        var docB = Guid.NewGuid();
        var report = new CompareReportDto
        {
            TaskId = Guid.NewGuid(),
            GeneratedAt = new DateTime(2026, 7, 29, 8, 0, 0, DateTimeKind.Utc),
            Summary = new ReportSummaryDto
            {
                DocCount = 2,
                HighRiskCount = 1,
                MidRiskCount = 0,
                LowRiskCount = 0,
                TopFindings = new List<string> { "标书A与标书B大段雷同" }
            },
            Matrix = new SimilarityMatrixDto
            {
                DocIds = new List<Guid> { docA, docB },
                Cells = new List<SimilarityMatrixCellDto>
                {
                    new() { DocAId = docA, DocBId = docA, Similarity = 1.0 },
                    new() { DocAId = docA, DocBId = docB, Similarity = 0.93 },
                    new() { DocAId = docB, DocBId = docA, Similarity = 0.93 },
                    new() { DocAId = docB, DocBId = docB, Similarity = 1.0 }
                }
            },
            Sections = new List<ReportSectionDto>
            {
                new()
                {
                    Key = "bidRiggingRisk",
                    Title = "围标风险",
                    Evidences = new List<EvidenceDto>
                    {
                        new()
                        {
                            Id = Guid.NewGuid(), TaskId = Guid.NewGuid(),
                            Type = EvidenceType.Similarity, Severity = EvidenceSeverity.High,
                            Title = "标书A与标书B大段雷同", Description = "第三章相似度 0.93",
                            AiGenerated = false
                        }
                    }
                }
            }
        };

        var bytes = await renderer.RenderAsync(report, "一期工程比标");

        bytes.Length.ShouldBeGreaterThan(100);
        bytes[0].ShouldBe((byte)'P'); // docx 即 zip，PK 头
        bytes[1].ShouldBe((byte)'K');

        using var document = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        var text = string.Concat(document.MainDocumentPart!.Document.Body!
            .Descendants<Text>().Select(t => t.Text));
        text.ShouldContain("一期工程比标");
        text.ShouldContain("标书A与标书B大段雷同");
        text.ShouldContain("0.93");
        text.ShouldContain("围标风险");
        text.ShouldNotContain("{{TaskName}}");
        text.ShouldNotContain("{{Conclusion}}");
    }
}
