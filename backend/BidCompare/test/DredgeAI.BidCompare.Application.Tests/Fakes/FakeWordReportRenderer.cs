using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Reports;

namespace DredgeAI.BidCompare.Reporting;

public class FakeWordReportRenderer : IWordReportRenderer
{
    public CompareReportDto? LastReport { get; private set; }

    public Task<byte[]> RenderAsync(CompareReportDto report, string taskName, CancellationToken cancellationToken = default)
    {
        LastReport = report;
        return Task.FromResult(System.Text.Encoding.ASCII.GetBytes("FAKE-DOCX-CONTENT"));
    }
}
