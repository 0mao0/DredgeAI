using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DredgeAI.BidCompare.Reporting;

/// <summary>
/// 生成报告 docx 模板（封面 + 摘要占位符）。占位符（每个独立成段，保证在单个 Run 内）：
/// {{TaskName}} {{GeneratedAt}} {{Conclusion}} {{DocCount}} {{HighCount}} {{MidCount}} {{LowCount}}
/// </summary>
public static class DocxReportTemplateBuilder
{
    public static string EnsureTemplate(string templatePath)
    {
        if (File.Exists(templatePath))
        {
            return templatePath;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(templatePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using (var document = WordprocessingDocument.Create(templatePath, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;

            body.Append(TemplateParagraph("比标分析报告", bold: true, fontSize: "44"));
            body.Append(TemplateParagraph("任务名称：{{TaskName}}"));
            body.Append(TemplateParagraph("生成时间：{{GeneratedAt}}"));
            body.Append(TemplateParagraph("总体结论：{{Conclusion}}"));
            body.Append(TemplateParagraph("标书份数：{{DocCount}}　高风险：{{HighCount}}　中风险：{{MidCount}}　低风险：{{LowCount}}"));
            body.Append(new SectionProperties());

            mainPart.Document.Save();
        }

        return templatePath;
    }

    private static Paragraph TemplateParagraph(string text, bool bold = false, string? fontSize = null)
    {
        var runProperties = new RunProperties();
        if (bold)
        {
            runProperties.Append(new Bold());
        }
        if (fontSize != null)
        {
            runProperties.Append(new FontSize { Val = fontSize });
        }
        var run = new Run(runProperties, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return new Paragraph(run);
    }
}
