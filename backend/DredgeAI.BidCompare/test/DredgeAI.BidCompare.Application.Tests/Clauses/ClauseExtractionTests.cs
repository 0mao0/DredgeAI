using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using DredgeAI.BidCompare.BackgroundJobs;
using DredgeAI.BidCompare.Clauses;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Xunit;

namespace DredgeAI.BidCompare.Clauses;

public class ClauseExtractionTests : BidCompareApplicationTestBase<BidCompareApplicationTestModule>
{
    private readonly ICompareTaskAppService _appService;
    private readonly FakeLlmGateway _llmGateway;
    private readonly RecordingBackgroundJobManager _jobManager;

    public ClauseExtractionTests()
    {
        _appService = GetRequiredService<ICompareTaskAppService>();
        _llmGateway = (FakeLlmGateway)GetRequiredService<ILlmGateway>();
        _jobManager = (RecordingBackgroundJobManager)GetRequiredService<IBackgroundJobManager>();
    }

    private async Task<(Guid TaskId, Guid TenderId, Guid BidId)> PrepareAwaitingClausesTaskAsync()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var tender = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Tender, "招标文件.pdf", new MemoryStream(new byte[] { 1 }));
        var bid = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf", new MemoryStream(new byte[] { 2 }));
        var parseJob = GetRequiredService<ParseDocumentJob>();
        await WithUnitOfWorkAsync(async () =>
        {
            await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = tender.Id });
            await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = bid.Id });
        });
        return (task.Id, tender.Id, bid.Id);
    }

    [Fact]
    public async Task Extract_Should_Return_Draft_Without_Persisting()
    {
        var (taskId, _, _) = await PrepareAwaitingClausesTaskAsync();
        _llmGateway.QueueResponse("""
        ```json
        [
          { "text": "投标人须具备建筑工程施工总承包一级资质", "mandatory": true, "category": "资质" },
          { "text": "工期不得超过 180 日历天", "mandatory": true, "category": "工期" }
        ]
        ```
        """);

        var drafts = await _appService.ExtractClausesAsync(taskId);

        drafts.Count.ShouldBe(2);
        drafts.ShouldAllBe(d => d.Source == ClauseSource.Extracted);
        drafts.ShouldAllBe(d => !string.IsNullOrWhiteSpace(d.ClauseId));
        drafts[0].Text.ShouldContain("总承包一级资质");
        drafts[0].Mandatory.ShouldBeTrue();
        drafts[1].Category.ShouldBe("工期");

        // 草案不落库：任务仍处于待确认，快照仍为空（spec §3.2：AI 提取不当黑盒）
        var detail = await _appService.GetAsync(taskId);
        detail.Status.ShouldBe(CompareTaskStatus.AwaitingClauses);
        detail.ClauseSnapshot.ShouldBeNull();

        // prompt 中应带招标文件 content.md 内容
        _llmGateway.Requests.Single().User.ShouldContain("第三章 技术方案");
    }

    [Fact]
    public async Task Extract_Without_TenderDoc_Should_Throw()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });

        var ex = await Should.ThrowAsync<BusinessException>(() => _appService.ExtractClausesAsync(task.Id));
        ex.Code.ShouldBe(BidCompareErrorCodes.NoTenderDocument);
    }

    [Fact]
    public async Task ConfirmClauses_Should_Lock_Snapshot_And_Start_Comparing()
    {
        var (taskId, _, _) = await PrepareAwaitingClausesTaskAsync();
        _jobManager.Clear();

        var result = await _appService.ConfirmClausesAsync(taskId, new ConfirmClausesInput
        {
            Clauses = new()
            {
                new ClauseInputDto { Text = "AI 草案条款", Source = ClauseSource.Extracted, Mandatory = true, Category = "资质" },
                new ClauseInputDto { Text = "手动补充条款", Mandatory = true },
                new ClauseInputDto { ClauseId = "tpl-001", Text = "条款库条款", Source = ClauseSource.Template, Mandatory = false }
            }
        });

        result.Status.ShouldBe(CompareTaskStatus.Comparing); // spec §5 步骤3→4
        result.ClauseSnapshot.ShouldNotBeNull();
        result.ClauseSnapshot!.Count.ShouldBe(3);
        result.ClauseSnapshot[0].Source.ShouldBe(ClauseSource.Extracted);
        result.ClauseSnapshot[1].Source.ShouldBe(ClauseSource.Manual);
        result.ClauseSnapshot[2].ClauseId.ShouldBe("tpl-001"); // 模板条款保留原 id
        result.ClauseSnapshot[2].Mandatory.ShouldBeFalse();

        _jobManager.LastEnqueued<CompareDocumentsArgs>().ShouldNotBeNull();

        // 快照已锁定：再次确认应被状态机拒绝
        await Should.ThrowAsync<BusinessException>(() =>
            _appService.ConfirmClausesAsync(taskId, new ConfirmClausesInput
            {
                Clauses = new() { new ClauseInputDto { Text = "x" } }
            }));
    }
}
