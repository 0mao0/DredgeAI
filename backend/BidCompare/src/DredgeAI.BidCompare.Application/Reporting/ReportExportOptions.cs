namespace DredgeAI.BidCompare.Reporting;

public class ReportExportOptions
{
    /// <summary>
    /// docx 模板路径。首次使用时若不存在由 DocxReportTemplateBuilder 自动生成；
    /// 正式商务风格模板（spec §11 待决事项4）可直接替换该文件，占位符保持不变。
    /// </summary>
    public string TemplatePath { get; set; } = "Templates/compare-report-template.docx";
}
