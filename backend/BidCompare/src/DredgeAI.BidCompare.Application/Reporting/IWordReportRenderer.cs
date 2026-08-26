using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Reports;

namespace DredgeAI.BidCompare.Reporting;

/// <summary>Word 报告渲染（OpenXML 基于 docx 模板填充）。</summary>
public interface IWordReportRenderer
{
    Task<byte[]> RenderAsync(CompareReportDto report, string taskName, CancellationToken cancellationToken = default);
}
