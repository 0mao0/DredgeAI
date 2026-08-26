using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using DredgeAI.BidCompare.BackgroundJobs;
using DredgeAI.BidCompare.Clauses;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Evidences;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace DredgeAI.BidCompare.Reports;

public class ReportTests : BidCompareApplicationTestBase<BidCompareApplicationTestModule>
{
    private readonly ICompareTaskAppService _appService;
    private readonly FakeLlmGateway _llmGateway;

    public ReportTests()
    {
        _appService = GetRequiredService<ICompareTaskAppService>();
        _llmGateway = (FakeLlmGateway)GetRequiredService<ILlmGateway>();
    }

    private async Task<Guid> PrepareDoneTaskAsync()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "一期比标" });
        var tender = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Tender, "招标文件.pdf", TestFiles.Pdf(0));
        var docA = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf", TestFiles.Pdf(1));
        var docB = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书B.pdf", TestFiles.Pdf(2));
        var parseJob = GetRequiredService<ParseDocumentJob>();
        await WithUnitOfWorkAsync(async () =>
        {
            await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = tender.Id });
            await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = docA.Id });
            await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = docB.Id });
        });
        await _appService.ConfirmClausesAsync(task.Id, new ConfirmClausesInput
        {
            Clauses = new() { new ClauseInputDto { ClauseId = "c1", Text = "须提供 ISO9001 证书", Mandatory = true } }
        });
        _llmGateway.QueueResponse("""[{"clauseId":"c1","status":"none","reason":"未提供证书","blockIds":["b0001"]}]""");
        _llmGateway.QueueResponse("""[{"clauseId":"c1","status":"responded","reason":"已提供","blockIds":[]}]""");
        _llmGateway.QueueResponse("""[]""");
        await WithUnitOfWorkAsync(async () =>
        {
            await GetRequiredService<CompareDocumentsJob>().ExecuteAsync(new CompareDocumentsArgs { TaskId = task.Id });
            await GetRequiredService<AiAnalysisJob>().ExecuteAsync(new AiAnalysisArgs { TaskId = task.Id });
        });
        return task.Id;
    }

    [Fact]
    public async Task Report_Should_Be_Assembled_And_Cached_After_Done()
    {
        var taskId = await PrepareDoneTaskAsync();

        var report = await _appService.GetReportAsync(taskId);

        report.TaskId.ShouldBe(taskId);
        report.GeneratedAt.ShouldBeGreaterThan(DateTime.MinValue);
        report.Summary.DocCount.ShouldBe(2);
        report.Summary.HighRiskCount.ShouldBe(1); // c1 未响应（mandatory）
        report.Summary.TopFindings.ShouldNotBeEmpty();
        report.Matrix.Cells.Count.ShouldBe(4);
        report.Sections.Select(s => s.Key).ShouldBe(
            new[] { "bidRiggingRisk", "clauseCompliance", "indicatorComparison" }, ignoreOrder: false);

        var clauseSection = report.Sections.Single(s => s.Key == "clauseCompliance");
        clauseSection.Title.ShouldBe("强制性条款响应");
        clauseSection.Evidences.Count.ShouldBe(1);
        clauseSection.Evidences[0].AiGenerated.ShouldBeTrue();

        // 缓存：二次读取反序列化自 CompareTask.ReportJson，结果一致
        var again = await _appService.GetReportAsync(taskId);
        again.GeneratedAt.ShouldBe(report.GeneratedAt);
        again.Summary.HighRiskCount.ShouldBe(1);
    }

    [Fact]
    public async Task Report_Before_Done_Should_Throw_ReportNotReady()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });

        var ex = await Should.ThrowAsync<BusinessException>(() => _appService.GetReportAsync(task.Id));
        ex.Code.ShouldBe(BidCompareErrorCodes.ReportNotReady);
    }
}
