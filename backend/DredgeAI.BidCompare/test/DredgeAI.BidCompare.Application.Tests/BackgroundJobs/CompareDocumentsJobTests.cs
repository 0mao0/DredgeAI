using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Analysis;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Evidences;
using Shouldly;
using Xunit;

namespace DredgeAI.BidCompare.BackgroundJobs;

public class CompareDocumentsJobTests : BidCompareApplicationTestBase<BidCompareApplicationTestModule>
{
    private readonly ICompareTaskAppService _appService;
    private readonly FakeCompareAlgoClient _algoClient;

    public CompareDocumentsJobTests()
    {
        _appService = GetRequiredService<ICompareTaskAppService>();
        _algoClient = (FakeCompareAlgoClient)GetRequiredService<ICompareAlgoClient>();
    }

    /// <summary>建 2 份标书并跑完解析，返回 (taskId, docAId, docBId)。</summary>
    private async Task<(Guid TaskId, Guid DocA, Guid DocB)> PrepareParsedTaskAsync()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var docA = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf", new MemoryStream(new byte[] { 1 }));
        var docB = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书B.pdf", new MemoryStream(new byte[] { 2 }));
        var parseJob = GetRequiredService<ParseDocumentJob>();
        await WithUnitOfWorkAsync(async () =>
        {
            await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = docA.Id });
            await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = docB.Id });
        });
        return (task.Id, docA.Id, docB.Id);
    }

    private void SetupAlgoEvidences(Guid docA, Guid docB)
    {
        _algoClient.SimilarityEvidences = new List<AlgoEvidence>
        {
            new()
            {
                Type = "similarity",
                Severity = "high",
                DocIds = new List<string> { docA.ToString(), docB.ToString() },
                Locations = new List<AlgoEvidenceLocation>
                {
                    new() { DocId = docA.ToString(), BlockIds = new List<string> { "b0001" } },
                    new() { DocId = docB.ToString(), BlockIds = new List<string> { "b0001" } }
                },
                Metrics = new Dictionary<string, JsonElement>
                {
                    ["similarity"] = JsonDocument.Parse("0.93").RootElement.Clone()
                },
                Title = "标书A与标书B大段雷同",
                Description = "第三章相似度 0.93"
            }
        };
        _algoClient.PricingEvidences = new List<AlgoEvidence>
        {
            new()
            {
                Type = "pricing",
                Severity = "mid",
                DocIds = new List<string> { docA.ToString(), docB.ToString() },
                Locations = new List<AlgoEvidenceLocation>(),
                Metrics = new Dictionary<string, JsonElement>(),
                Title = "报价呈等差规律",
                Description = "两份报价差值固定 1000 元"
            }
        };
    }

    private async Task RunCompareJobAsync(Guid taskId)
    {
        var job = GetRequiredService<CompareDocumentsJob>();
        await WithUnitOfWorkAsync(async () =>
        {
            await job.ExecuteAsync(new CompareDocumentsArgs { TaskId = taskId });
        });
    }

    [Fact]
    public async Task Compare_Job_Should_Persist_Evidences_And_Finish_Task()
    {
        var (taskId, docA, docB) = await PrepareParsedTaskAsync();
        SetupAlgoEvidences(docA, docB);

        await RunCompareJobAsync(taskId);

        var detail = await _appService.GetAsync(taskId);
        detail.Status.ShouldBe(CompareTaskStatus.Done); // P1 无 AI 阶段，比对完成即 Done
        detail.Progress.Percent.ShouldBe(100);

        var list = await _appService.GetEvidencesAsync(taskId, new GetEvidenceListInput { MaxResultCount = 10 });
        list.TotalCount.ShouldBe(2);
        list.Items.ShouldAllBe(e => e.AiGenerated == false);

        var similarity = list.Items.Single(e => e.Type == EvidenceType.Similarity);
        similarity.Severity.ShouldBe(EvidenceSeverity.High);
        similarity.DocIds.ShouldBe(new[] { docA, docB }, ignoreOrder: true);
        similarity.Locations.Count.ShouldBe(2);
        similarity.Locations[0].BlockIds.ShouldContain("b0001");
        similarity.Metrics.ShouldNotBeNull();
        similarity.Metrics!.Similarity.ShouldBe(0.93);
        similarity.Title.ShouldBe("标书A与标书B大段雷同");
    }

    [Fact]
    public async Task Evidences_Should_Filter_By_Type_Severity_And_DocPair()
    {
        var (taskId, docA, docB) = await PrepareParsedTaskAsync();
        SetupAlgoEvidences(docA, docB);
        await RunCompareJobAsync(taskId);

        var byType = await _appService.GetEvidencesAsync(taskId,
            new GetEvidenceListInput { Type = EvidenceType.Pricing, MaxResultCount = 10 });
        byType.TotalCount.ShouldBe(1);

        var bySeverity = await _appService.GetEvidencesAsync(taskId,
            new GetEvidenceListInput { Severity = EvidenceSeverity.High, MaxResultCount = 10 });
        bySeverity.TotalCount.ShouldBe(1);

        var byPair = await _appService.GetEvidencesAsync(taskId,
            new GetEvidenceListInput { DocIdA = docA, DocIdB = docB, MaxResultCount = 10 });
        byPair.TotalCount.ShouldBe(2);

        var byPairMiss = await _appService.GetEvidencesAsync(taskId,
            new GetEvidenceListInput { DocIdA = docA, DocIdB = Guid.NewGuid(), MaxResultCount = 10 });
        byPairMiss.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Matrix_Should_Be_NxN_With_Diagonal_One()
    {
        var (taskId, docA, docB) = await PrepareParsedTaskAsync();
        SetupAlgoEvidences(docA, docB);
        await RunCompareJobAsync(taskId);

        var matrix = await _appService.GetMatrixAsync(taskId);

        matrix.DocIds.ShouldBe(new[] { docA, docB });
        matrix.Cells.Count.ShouldBe(4); // N×N = 2×2
        matrix.Cells.Single(c => c.DocAId == docA && c.DocBId == docA).Similarity.ShouldBe(1.0);
        matrix.Cells.Single(c => c.DocAId == docA && c.DocBId == docB).Similarity.ShouldBe(0.93);
        matrix.Cells.Single(c => c.DocAId == docB && c.DocBId == docA).Similarity.ShouldBe(0.93);
    }

    [Fact]
    public async Task Algo_Service_Unavailable_Should_Mark_Task_Failed()
    {
        // spec §9：不静默降级，明确提示
        var (taskId, _, _) = await PrepareParsedTaskAsync();
        _algoClient.FailWith = "connection refused";

        await RunCompareJobAsync(taskId);

        var detail = await _appService.GetAsync(taskId);
        detail.Status.ShouldBe(CompareTaskStatus.Failed);
    }

    [Fact]
    public async Task Less_Than_Two_Parsed_Bids_Should_Fail_Task()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var docA = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf", new MemoryStream(new byte[] { 1 }));
        await WithUnitOfWorkAsync(async () =>
        {
            await GetRequiredService<ParseDocumentJob>().ExecuteAsync(
                new ParseDocumentArgs { TaskId = task.Id, DocumentId = docA.Id });
        });
        // 只有 1 份解析成功（手动触发比对，模拟边界）
        await RunCompareJobAsync(task.Id);

        var detail = await _appService.GetAsync(task.Id);
        detail.Status.ShouldBe(CompareTaskStatus.Failed);
    }
}
