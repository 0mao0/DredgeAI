using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DredgeAI.BidCompare.Evidences;
using DredgeAI.BidCompare.Reports;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.Reporting;

/// <summary>OpenXML 报告渲染：模板占位符替换（封面/摘要）+ 追加矩阵表与三节证据（spec §8 结构）。</summary>
public class OpenXmlWordReportRenderer : IWordReportRenderer, ITransientDependency
{
    private readonly ReportExportOptions _options;

    public OpenXmlWordReportRenderer(IOptions<ReportExportOptions> options)
    {
        _options = options.Value;
    }

    public Task<byte[]> RenderAsync(CompareReportDto report, string taskName, CancellationToken cancellationToken = default)
    {
        var templatePath = DocxReportTemplateBuilder.EnsureTemplate(_options.TemplatePath);
        var templateBytes = File.ReadAllBytes(templatePath);

        using var stream = new MemoryStream();
        stream.Write(templateBytes, 0, templateBytes.Length);

        using (var document = WordprocessingDocument.Open(stream, true))
        {
            var body = document.MainDocumentPart!.Document.Body!;

            ReplaceTokens(body, new Dictionary<string, string>
            {
                ["{{TaskName}}"] = taskName,
                ["{{GeneratedAt}}"] = report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                ["{{Conclusion}}"] = BuildConclusion(report),
                ["{{DocCount}}"] = report.Summary.DocCount.ToString(),
                ["{{HighCount}}"] = report.Summary.HighRiskCount.ToString(),
                ["{{MidCount}}"] = report.Summary.MidRiskCount.ToString(),
                ["{{LowCount}}"] = report.Summary.LowRiskCount.ToString()
            });

            var sectPr = body.Elements<SectionProperties>().FirstOrDefault();
            void Append(OpenXmlElement element)
            {
                if (sectPr != null)
                {
                    body.InsertBefore(element, sectPr);
                }
                else
                {
                    body.Append(element);
                }
            }

            // spec §8-2 摘要
            Append(Heading1("一、摘要"));
            if (report.Summary.TopFindings.Count == 0)
            {
                Append(Para("无重大发现。"));
            }
            foreach (var finding in report.Summary.TopFindings)
            {
                Append(Para("• " + finding));
            }

            // spec §8-3 相似度矩阵
            Append(Heading1("二、相似度矩阵"));
            Append(BuildMatrixTable(report));

            // spec §8-4/5/6 三节详情
            var numerals = new[] { "三", "四", "五" };
            for (var i = 0; i < report.Sections.Count && i < numerals.Length; i++)
            {
                var section = report.Sections[i];
                Append(Heading1($"{numerals[i]}、{section.Title}"));
                if (section.Evidences.Count == 0)
                {
                    Append(Para("无。"));
                }
                foreach (var evidence in section.Evidences)
                {
                    Append(Para($"【{SeverityText(evidence.Severity)}】{evidence.Title}", bold: true));
                    Append(Para(evidence.Description));
                    if (evidence.AiGenerated)
                    {
                        Append(Para("（AI 分析）")); // spec §8-4：AI 生成的判断标注「AI 分析」
                    }
                }
            }

            // spec §8-7 附录
            Append(Heading1("六、附录"));
            Append(Para("条款清单快照与解析质量说明以系统内任务数据为准。"));
            Append(Para("免责声明：本报告由 AI 投标-比标系统自动生成，结论供评审参考，不构成最终评标依据。"));

            document.MainDocumentPart.Document.Save();
        }

        return Task.FromResult(stream.ToArray());
    }

    private static string BuildConclusion(CompareReportDto report)
    {
        if (report.Summary.HighRiskCount > 0)
        {
            return $"发现 {report.Summary.HighRiskCount} 项高风险问题，存在围串标嫌疑，建议重点核查。";
        }
        if (report.Summary.MidRiskCount > 0)
        {
            return $"未发现高风险问题；存在 {report.Summary.MidRiskCount} 项中风险事项，建议关注。";
        }
        return "未发现明显围串标嫌疑。";
    }

    private static string SeverityText(EvidenceSeverity severity) => severity switch
    {
        EvidenceSeverity.High => "高风险",
        EvidenceSeverity.Mid => "中风险",
        _ => "低风险"
    };

    private static void ReplaceTokens(Body body, Dictionary<string, string> tokens)
    {
        foreach (var text in body.Descendants<Text>())
        {
            foreach (var (token, value) in tokens)
            {
                if (text.Text.Contains(token))
                {
                    text.Text = text.Text.Replace(token, value);
                }
            }
        }
    }

    private static Paragraph Heading1(string text)
    {
        var run = new Run(new RunProperties(new Bold(), new FontSize { Val = "32" }), new Text(text));
        return new Paragraph(run);
    }

    private static Paragraph Para(string text, bool bold = false)
    {
        var run = new Run();
        if (bold)
        {
            run.Append(new RunProperties(new Bold()));
        }
        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return new Paragraph(run);
    }

    private static Table BuildMatrixTable(CompareReportDto report)
    {
        var table = new Table(
            new TableProperties(
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4 },
                    new BottomBorder { Val = BorderValues.Single, Size = 4 },
                    new LeftBorder { Val = BorderValues.Single, Size = 4 },
                    new RightBorder { Val = BorderValues.Single, Size = 4 },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 })));

        var headerCells = new List<TableCell> { Cell("A\\B") };
        headerCells.AddRange(report.Matrix.DocIds.Select(id => Cell(ShortId(id))));
        table.Append(new TableRow(headerCells.ToArray()));

        foreach (var a in report.Matrix.DocIds)
        {
            var row = new List<TableCell> { Cell(ShortId(a)) };
            foreach (var b in report.Matrix.DocIds)
            {
                var cell = report.Matrix.Cells.First(c => c.DocAId == a && c.DocBId == b);
                row.Add(Cell(cell.Similarity.ToString("0.00")));
            }
            table.Append(new TableRow(row.ToArray()));
        }

        return table;
    }

    private static TableCell Cell(string text)
        => new(new Paragraph(new Run(new Text(text ?? "") { Space = SpaceProcessingModeValues.Preserve })));

    private static string ShortId(Guid id) => id.ToString("N")[..8];
}
