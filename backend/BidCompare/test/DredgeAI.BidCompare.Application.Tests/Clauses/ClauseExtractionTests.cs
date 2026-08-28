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
        var tender = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Tender, "招标文件.pdf", TestFiles.Pdf(1));
        var bid = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf", TestFiles.Pdf(2));
        var parseJob = GetRequiredService<ParseDocumentJob>();
        await WithUnitOfWorkAsync(async () =>
        {
            await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = tender.Id });
            await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = bid.Id });
        });
        return (task.Id, tender.Id, bid.Id);
    }

    [Fact]
    public async Task Extract_Should_Enqueue_Job_And_Return_Task()
    {
        var (taskId, _, _) = await PrepareAwaitingClausesTaskAsync();
        _jobManager.Clear();

        var result = await _appService.ExtractClausesAsync(taskId);

        // 异步触发：仅入队后台作业并更新进度，草案由轮询感知
        _jobManager.LastEnqueued<ExtractClausesArgs>()!.TaskId.ShouldBe(taskId);
        result.Progress.Stage.ShouldBe("clauses_extracting");
        result.ClauseDrafts.ShouldBeNull();
        result.Status.ShouldBe(CompareTaskStatus.AwaitingClauses);
    }

    [Fact]
    public async Task ExtractJob_Should_Generate_Drafts_Without_Locking_Snapshot()
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

        var job = GetRequiredService<ExtractClausesJob>();
        await WithUnitOfWorkAsync(async () =>
        {
            await job.ExecuteAsync(new ExtractClausesArgs { TaskId = taskId });
        });

        // 草案挂在任务 DTO 上，状态保持待确认、快照仍为空（spec §3.2：AI 提取不当黑盒）
        var detail = await _appService.GetAsync(taskId);
        detail.ClauseDrafts.ShouldNotBeNull();
        detail.ClauseDrafts!.Count.ShouldBe(2);
        detail.ClauseDrafts.ShouldAllBe(d => d.Source == ClauseSource.Extracted);
        detail.ClauseDrafts.ShouldAllBe(d => !string.IsNullOrWhiteSpace(d.ClauseId));
        detail.ClauseDrafts[0].Text.ShouldContain("总承包一级资质");
        detail.ClauseDrafts[0].Mandatory.ShouldBeTrue();
        detail.ClauseDrafts[1].Category.ShouldBe("工期");
        detail.Status.ShouldBe(CompareTaskStatus.AwaitingClauses);
        detail.ClauseSnapshot.ShouldBeNull();
        detail.Progress.Stage.ShouldBe("clauses");

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
