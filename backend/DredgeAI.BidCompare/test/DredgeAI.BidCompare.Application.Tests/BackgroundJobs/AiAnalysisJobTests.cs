using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using DredgeAI.BidCompare.Clauses;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Evidences;
using Shouldly;
using Xunit;

namespace DredgeAI.BidCompare.BackgroundJobs;

public class AiAnalysisJobTests : BidCompareApplicationTestBase<BidCompareApplicationTestModule>
{
    private readonly ICompareTaskAppService _appService;
    private readonly FakeLlmGateway _llmGateway;

    public AiAnalysisJobTests()
    {
        _appService = GetRequiredService<ICompareTaskAppService>();
        _llmGateway = (FakeLlmGateway)GetRequiredService<ILlmGateway>();
    }

    /// <summary>建 2 份标书 + 条款快照 → 解析 → 确认条款 → 比对，任务进入 Analyzing 并排好 AI 证据的 LLM 响应。</summary>
    private async Task<(Guid TaskId, Guid DocA, Guid DocB)> PrepareAnalyzingTaskAsync()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var tender = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Tender, "招标文件.pdf", new MemoryStream(new byte[] { 0 }));
        var docA = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf", new MemoryStream(new byte[] { 1 }));
        var docB = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书B.pdf", new MemoryStream(new byte[] { 2 }));
        var parseJob = GetRequiredService<ParseDocumentJob>();
        await WithUnitOfWorkAsync(async () =>
        {
            await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = tender.Id });
            await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = docA.Id });
            await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = docB.Id });
        });

        await _appService.ConfirmClausesAsync(task.Id, new ConfirmClausesInput
        {
            Clauses = new()
            {
                new ClauseInputDto { ClauseId = "c1", Text = "须提供 ISO9001 证书", Mandatory = true, Category = "资质" }
            }
        });

        // 条款判定：docA 未响应（High），docB 部分响应（Mid）；随后指标抽取一次
        _llmGateway.QueueResponse("""[{"clauseId":"c1","status":"none","reason":"全文未提及质量管理体系认证","blockIds":["b0001"]}]""");
        _llmGateway.QueueResponse("""[{"clauseId":"c1","status":"partial","reason":"仅承诺投标后补办","blockIds":["b0003"]}]""");
        _llmGateway.QueueResponse("""[{"indicator":"报价","summaries":[{"docId":"DOC_A","summary":"总价 120 万元"},{"docId":"DOC_B","summary":"总价 118 万元"}]}]"""
            .Replace("DOC_A", docA.Id.ToString()).Replace("DOC_B", docB.Id.ToString()));

        await WithUnitOfWorkAsync(async () =>
        {
            await GetRequiredService<CompareDocumentsJob>().ExecuteAsync(new CompareDocumentsArgs { TaskId = task.Id });
        });
        return (task.Id, docA.Id, docB.Id);
    }

    private async Task RunAiAnalysisAsync(Guid taskId)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await GetRequiredService<AiAnalysisJob>().ExecuteAsync(new AiAnalysisArgs { TaskId = taskId });
        });
    }

    [Fact]
    public async Task AiAnalysis_Should_Persist_Clause_And_Indicator_Evidences_Then_Done()
    {
        var (taskId, docA, docB) = await PrepareAnalyzingTaskAsync();

        await RunAiAnalysisAsync(taskId);

        var detail = await _appService.GetAsync(taskId);
        detail.Status.ShouldBe(CompareTaskStatus.Done);
        detail.Progress.Percent.ShouldBe(100);

        var clauseEvidences = await _appService.GetEvidencesAsync(taskId,
            new GetEvidenceListInput { Type = EvidenceType.Clause, MaxResultCount = 10 });
        clauseEvidences.TotalCount.ShouldBe(2);
        clauseEvidences.Items.ShouldAllBe(e => e.AiGenerated); // spec §3.2：AI 结论可区分

        var high = clauseEvidences.Items.Single(e => e.Severity == EvidenceSeverity.High);
        high.DocIds.ShouldBe(new[] { docA });
        high.Locations.Single().DocId.ShouldBe(docA);
        high.Locations.Single().BlockIds.ShouldContain("b0001");
        high.Description.ShouldContain("质量管理体系认证");

        var mid = clauseEvidences.Items.Single(e => e.Severity == EvidenceSeverity.Mid);
        mid.DocIds.ShouldBe(new[] { docB });

        var indicatorEvidences = await _appService.GetEvidencesAsync(taskId,
            new GetEvidenceListInput { Type = EvidenceType.Indicator, MaxResultCount = 10 });
        indicatorEvidences.TotalCount.ShouldBe(1);
        indicatorEvidences.Items[0].Title.ShouldContain("报价");
        indicatorEvidences.Items[0].Description.ShouldContain("120 万元");
        indicatorEvidences.Items[0].AiGenerated.ShouldBeTrue();
    }

    [Fact]
    public async Task Llm_Failure_Should_Not_Block_Task() // spec §9：AI 失败不阻塞整体
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var tender = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Tender, "招标文件.pdf", new MemoryStream(new byte[] { 0 }));
        var docA = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf", new MemoryStream(new byte[] { 1 }));
        var docB = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书B.pdf", new MemoryStream(new byte[] { 2 }));
        var parseJob = GetRequiredService<ParseDocumentJob>();
        await WithUnitOfWorkAsync(async () =>
        {
            await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = tender.Id });
            await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = docA.Id });
            await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = docB.Id });
        });
        await _appService.ConfirmClausesAsync(task.Id, new ConfirmClausesInput
        {
            Clauses = new() { new ClauseInputDto { Text = "x", Mandatory = true } }
        });
        await WithUnitOfWorkAsync(async () =>
        {
            await GetRequiredService<CompareDocumentsJob>().ExecuteAsync(new CompareDocumentsArgs { TaskId = task.Id });
        });
        // 不 QueueResponse → FakeLlmGateway 抛 InvalidOperationException，模拟 AI 服务失败

        await RunAiAnalysisAsync(task.Id);

        var detail = await _appService.GetAsync(task.Id);
        detail.Status.ShouldBe(CompareTaskStatus.Done); // 算法证据照常展示
        detail.Progress.Message.ShouldContain("AI 分析暂不可用");
    }
}
