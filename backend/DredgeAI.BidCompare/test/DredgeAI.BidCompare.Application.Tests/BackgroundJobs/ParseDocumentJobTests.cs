using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.AnGineer;
using DredgeAI.BidCompare.Storage;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Xunit;

namespace DredgeAI.BidCompare.BackgroundJobs;

public class ParseDocumentJobTests : BidCompareApplicationTestBase<BidCompareApplicationTestModule>
{
    private readonly ICompareTaskAppService _appService;
    private readonly RecordingBackgroundJobManager _jobManager;
    private readonly InMemoryFileStorage _fileStorage;
    private readonly FakeAnGineerClient _anGineerClient;

    public ParseDocumentJobTests()
    {
        _appService = GetRequiredService<ICompareTaskAppService>();
        _jobManager = (RecordingBackgroundJobManager)GetRequiredService<IBackgroundJobManager>();
        _fileStorage = (InMemoryFileStorage)GetRequiredService<IFileStorage>();
        _anGineerClient = (FakeAnGineerClient)GetRequiredService<IAnGineerClient>();
    }

    private async Task<(CompareTaskDto Task, CompareDocumentDto Doc)> CreateTaskWithBidDocAsync(
        string fileName = "标书A.pdf")
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var doc = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, fileName,
            new MemoryStream(Encoding.UTF8.GetBytes("%PDF")));
        return (task, doc);
    }

    private async Task RunParseJobAsync(Guid taskId, Guid documentId)
    {
        var job = GetRequiredService<ParseDocumentJob>();
        // 生产环境由 ABP BackgroundJobWorker 在 UnitOfWork 中执行；测试同样包一层
        await WithUnitOfWorkAsync(async () =>
        {
            await job.ExecuteAsync(new ParseDocumentArgs { TaskId = taskId, DocumentId = documentId });
        });
    }

    [Fact]
    public async Task Successful_Parse_Should_Store_Ir_Package_And_Advance_State()
    {
        var (task, doc) = await CreateTaskWithBidDocAsync();
        _jobManager.Clear();

        await RunParseJobAsync(task.Id, doc.Id);

        var detail = await _appService.GetAsync(task.Id);
        detail.Status.ShouldBe(CompareTaskStatus.Parsed); // v2：可用标书不足 2 份，等待继续上传
        detail.SuggestedName.ShouldNotBeNullOrWhiteSpace(); // 解析完成回填建议名
        detail.NameEditedByUser.ShouldBeFalse();
        _fileStorage.Objects.Keys.ShouldContain($"compare/{task.Id}/{doc.Id}/ir.json"); // 内部适配 IR（v2 映射后）
        _fileStorage.Objects.Keys.ShouldContain($"compare/{task.Id}/{doc.Id}/content.md");
        _fileStorage.Objects.Keys.ShouldContain($"compare/{task.Id}/{doc.Id}/images/t1.jpg");
        _fileStorage.Objects.Keys.ShouldContain($"compare/{task.Id}/{doc.Id}/raw/doc_blocks_graph.jsonl"); // AnGIneer 原始产物留档
        _jobManager.LastEnqueued<CompareDocumentsArgs>().ShouldBeNull();

        // AnGIneer 进度/阶段/耗时落库（v2 进度透传）
        var docRepo = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<CompareDocument, Guid>>();
        var parsedDoc = await docRepo.GetAsync(doc.Id);
        parsedDoc.ParseProgress.ShouldBe(100);
        parsedDoc.ParseStage.ShouldBe("completed");
        parsedDoc.ParseStageMessage.ShouldContain("解析结束");
        parsedDoc.ParseStartedAt.ShouldNotBeNull();
        parsedDoc.ParseFinishedAt.ShouldNotBeNull();
        var parseDuration = parsedDoc.ParseFinishedAt!.Value - parsedDoc.ParseStartedAt!.Value;
        parseDuration.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);

        // IR API 可读取（spec §6 GET ir；内容为内部适配形态）
        var ir = await _appService.GetDocumentIrAsync(task.Id, doc.Id);
        ir.DocId.ShouldBe(doc.Id.ToString()); // 内部适配 IR 的 docId 为本系统文档 id
        ir.Meta.FileName.ShouldBe("标书A.pdf");
        ir.Blocks.Count.ShouldBe(3);
        ir.Blocks[1].Table.ShouldNotBeNull();
        ir.Blocks[0].Bbox.ShouldBe(new double[] { 0.0672, 0.0594, 0.9244, 0.095 }); // 0~1 归一化（v2 §2）

        // 第二份标书解析完成 → 自动进入比对
        var docB = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书B.pdf",
            TestFiles.Pdf(2));
        await RunParseJobAsync(task.Id, docB.Id);

        var afterTwo = await _appService.GetAsync(task.Id);
        afterTwo.Status.ShouldBe(CompareTaskStatus.Comparing);
        _jobManager.LastEnqueued<CompareDocumentsArgs>().ShouldNotBeNull();
    }

    [Fact]
    public async Task Task_With_TenderDoc_Should_Wait_For_Clause_Confirmation()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var tender = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Tender, "招标文件.pdf",
            TestFiles.Pdf(1));
        var bid = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf",
            TestFiles.Pdf(1));

        await RunParseJobAsync(task.Id, tender.Id);
        await RunParseJobAsync(task.Id, bid.Id);

        var detail = await _appService.GetAsync(task.Id);
        detail.Status.ShouldBe(CompareTaskStatus.AwaitingClauses); // spec §5 步骤3：待条款确认
    }

    [Fact]
    public async Task AnGIneer_Failure_Should_Mark_Document_Failed_And_Task_Failed_When_All_Fail()
    {
        _anGineerClient.FailWith = "服务不可用";
        var (task, doc) = await CreateTaskWithBidDocAsync();

        await RunParseJobAsync(task.Id, doc.Id);

        var detail = await _appService.GetAsync(task.Id);
        detail.Status.ShouldBe(CompareTaskStatus.Failed); // spec §9：不静默降级，明确提示

        var docRepo = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<CompareDocument, Guid>>();
        var failed = await docRepo.GetAsync(doc.Id);
        failed.ParseStatus.ShouldBe(DocumentParseStatus.Failed);
        failed.ParseError.ShouldNotBeNullOrWhiteSpace();
        failed.ParseProgress.ShouldBe(100);
        failed.ParseStage.ShouldBe("failed");
        failed.ParseStartedAt.ShouldNotBeNull();
        failed.ParseFinishedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Transient_State_Poll_Error_Should_Not_Fail_Document()
    {
        _anGineerClient.TransientStateFailuresRemaining = 2;
        var (task, doc) = await CreateTaskWithBidDocAsync();

        await RunParseJobAsync(task.Id, doc.Id);

        var detail = await _appService.GetAsync(task.Id);
        detail.Status.ShouldBe(CompareTaskStatus.Parsed); // 单份标书解析完成，等第二份
        _fileStorage.Objects.Keys.ShouldContain($"compare/{task.Id}/{doc.Id}/ir.json");
    }

    [Fact]
    public async Task AnGIneer_Partial_With_Artifacts_Should_Parse_Successfully()
    {
        // docs-api soft 阶段（vectors/graph 等）失败时返回 partial，但 jsonl/meta 等核心产物仍完整；
        // DredgeAI 应按成功继续下载产物并落库，而不是当作未知状态一直轮询。
        _anGineerClient.StateSequence = new ConcurrentQueue<AnGineerJobStatus>(new[]
        {
            new AnGineerJobStatus(AnGineerJobState.Partial, 100, "partial", "解析结束: partial（向量阶段降级）")
        });
        var (task, doc) = await CreateTaskWithBidDocAsync();

        await RunParseJobAsync(task.Id, doc.Id);

        var detail = await _appService.GetAsync(task.Id);
        detail.Status.ShouldBe(CompareTaskStatus.Parsed);
        _fileStorage.Objects.Keys.ShouldContain($"compare/{task.Id}/{doc.Id}/ir.json");

        var docRepo = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<CompareDocument, Guid>>();
        var parsed = await docRepo.GetAsync(doc.Id);
        parsed.ParseStatus.ShouldBe(DocumentParseStatus.Parsed);
        parsed.ParseStage.ShouldBe("partial");
        parsed.ParseStageMessage.ShouldContain("解析结束: partial");
    }

    [Fact]
    public async Task AnGIneer_Partial_Without_Core_Artifacts_Should_Fail()
    {
        // partial 但核心结构产物缺失：不能静默当成解析成功，应明确失败并提示原因。
        _anGineerClient.StateSequence = new ConcurrentQueue<AnGineerJobStatus>(new[]
        {
            new AnGineerJobStatus(AnGineerJobState.Partial, 100, "partial", "解析结束: partial")
        });
        _anGineerClient.MissingArtifacts.Add("doc_blocks_graph.jsonl");
        _anGineerClient.MissingArtifacts.Add("doc_blocks_graph_meta.json");
        var (task, doc) = await CreateTaskWithBidDocAsync();

        await RunParseJobAsync(task.Id, doc.Id);

        var docRepo = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<CompareDocument, Guid>>();
        var failed = await docRepo.GetAsync(doc.Id);
        failed.ParseStatus.ShouldBe(DocumentParseStatus.Failed);
        failed.ParseError.ShouldContain("产物包缺少");
    }

    [Fact]
    public async Task Stale_Processing_Record_Should_Resume_And_Recover()
    {
        // docs-api 重启遗留的 processing 记录：progress=0 + 空阶段消息。
        // 新语义下不应直接判失败，而是 resume 后继续轮询到终态。
        _anGineerClient.StateSequence = new ConcurrentQueue<AnGineerJobStatus>(new[]
        {
            new AnGineerJobStatus(AnGineerJobState.Processing, 0, "processing", null),
            new AnGineerJobStatus(AnGineerJobState.Succeeded, 100, "completed", "解析结束: completed")
        });
        var (task, doc) = await CreateTaskWithBidDocAsync();

        await RunParseJobAsync(task.Id, doc.Id);

        var detail = await _appService.GetAsync(task.Id);
        detail.Status.ShouldBe(CompareTaskStatus.Parsed);
        _anGineerClient.ResumeCount.ShouldBeGreaterThanOrEqualTo(1);
        _fileStorage.Objects.Keys.ShouldContain($"compare/{task.Id}/{doc.Id}/ir.json");
    }

    [Fact]
    public async Task Failed_Reparse_Interruption_Should_Resume_Existing_Doc_Instead_Of_Reupload()
    {
        // “服务重启中断”类失败（docs-api 重启遗留）仍应复用 AnGIneer doc_id 走 resume，避免重复上传
        const string interruptionMessage = "服务重启导致解析中断，可调用 /api/v1/documents/fake-job/resume 恢复";
        _anGineerClient.FailWith = interruptionMessage;
        var (task, doc) = await CreateTaskWithBidDocAsync();
        await RunParseJobAsync(task.Id, doc.Id);

        var docRepo = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<CompareDocument, Guid>>();
        var failed = await docRepo.GetAsync(doc.Id);
        failed.ParseStatus.ShouldBe(DocumentParseStatus.Failed);
        _anGineerClient.SubmitCount.ShouldBe(1);

        _anGineerClient.FailWith = null;
        _anGineerClient.StateSequence = new ConcurrentQueue<AnGineerJobStatus>(new[]
        {
            new AnGineerJobStatus(AnGineerJobState.Failed, 100, "failed", interruptionMessage),
            new AnGineerJobStatus(AnGineerJobState.Succeeded, 100, "completed", "恢复后完成")
        });
        await _appService.ReparseAsync(task.Id, new ReparseDocumentsInput { DocIds = new() { doc.Id } });
        await RunParseJobAsync(task.Id, doc.Id);

        var parsed = await docRepo.GetAsync(doc.Id);
        parsed.ParseStatus.ShouldBe(DocumentParseStatus.Parsed);
        parsed.AnGineerDocId.ShouldNotBeNullOrWhiteSpace();
        _anGineerClient.SubmitCount.ShouldBe(1); // 未重新上传
        _anGineerClient.ResumeCount.ShouldBeGreaterThanOrEqualTo(1);
        _fileStorage.Objects.Keys.ShouldContain($"compare/{task.Id}/{doc.Id}/ir.json");
    }

    [Fact]
    public async Task Failed_Reparse_With_Generic_Failure_Should_Reupload()
    {
        // 2026-08-19 事故：AnGIneer 记录处于普通失败态（error 为空，resume 救不活），
        // 重新解析必须真正重新上传产生新的解析请求，而不是反复 resume 同一根死线。
        _anGineerClient.FailWith = "OCR 崩溃";
        var (task, doc) = await CreateTaskWithBidDocAsync();
        await RunParseJobAsync(task.Id, doc.Id);

        var docRepo = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<CompareDocument, Guid>>();
        var failed = await docRepo.GetAsync(doc.Id);
        failed.ParseStatus.ShouldBe(DocumentParseStatus.Failed);
        _anGineerClient.SubmitCount.ShouldBe(1);

        _anGineerClient.FailWith = null;
        _anGineerClient.StateSequence = new ConcurrentQueue<AnGineerJobStatus>(new[]
        {
            new AnGineerJobStatus(AnGineerJobState.Failed, 100, "failed", "解析结束: failed"),
            new AnGineerJobStatus(AnGineerJobState.Succeeded, 100, "completed", "恢复后完成")
        });
        await _appService.ReparseAsync(task.Id, new ReparseDocumentsInput { DocIds = new() { doc.Id } });
        await RunParseJobAsync(task.Id, doc.Id);

        var parsed = await docRepo.GetAsync(doc.Id);
        parsed.ParseStatus.ShouldBe(DocumentParseStatus.Parsed);
        parsed.AnGineerDocId.ShouldNotBeNullOrWhiteSpace();
        _anGineerClient.SubmitCount.ShouldBe(2); // 重新上传，产生新的解析请求
        _anGineerClient.ResumeCount.ShouldBe(0); // 普通失败态不再 resume
        _fileStorage.Objects.Keys.ShouldContain($"compare/{task.Id}/{doc.Id}/ir.json");
    }

    [Fact]
    public async Task Live_Processing_Stage_Should_Not_Be_Treated_As_Stale()
    {
        // 正常解析中的任务带真实阶段与消息（raw_parse + MinerU 解析中），不应触发悬挂保护。
        _anGineerClient.StateSequence = new ConcurrentQueue<AnGineerJobStatus>(new[]
        {
            new AnGineerJobStatus(AnGineerJobState.Processing, 0, "raw_parse", "MinerU 解析中"),
            new AnGineerJobStatus(AnGineerJobState.Succeeded, 100, "completed", "解析结束: completed")
        });
        var (task, doc) = await CreateTaskWithBidDocAsync();

        await RunParseJobAsync(task.Id, doc.Id);

        var detail = await _appService.GetAsync(task.Id);
        detail.Status.ShouldBe(CompareTaskStatus.Parsed);
        _fileStorage.Objects.Keys.ShouldContain($"compare/{task.Id}/{doc.Id}/ir.json");
    }

    [Fact]
    public async Task Partial_Failure_With_Less_Than_Two_Bids_Should_Wait_For_Reparse()
    {
        // v2 §5.4：失败导致可用标书 < 2 份时，在两两对比之前停留并提示重新解析失败文档
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var good = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf",
            TestFiles.Pdf(1));
        var bad = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书B.pdf",
            TestFiles.Pdf(1));

        await RunParseJobAsync(task.Id, good.Id);

        _anGineerClient.FailWith = "OCR 崩溃";
        await RunParseJobAsync(task.Id, bad.Id);

        var detail = await _appService.GetAsync(task.Id);
        detail.Status.ShouldBe(CompareTaskStatus.Partial);
        detail.Progress.Message.ShouldContain("可用标书不足 2 份");
        _jobManager.LastEnqueued<CompareDocumentsArgs>().ShouldBeNull();

        // 重新解析失败文档成功后不自动重跑全量对比（v2 §5.3），由用户显式「重新对比」
        await _appService.ReparseAsync(task.Id, new ReparseDocumentsInput { DocIds = new() { bad.Id } });
        _anGineerClient.FailWith = null;
        _jobManager.Clear();
        await RunParseJobAsync(task.Id, bad.Id);

        var afterReparse = await _appService.GetAsync(task.Id);
        afterReparse.Status.ShouldBe(CompareTaskStatus.Parsed);
        afterReparse.Progress.Message.ShouldContain("等待重新对比");
        _jobManager.LastEnqueued<CompareDocumentsArgs>().ShouldBeNull();
    }

    [Fact]
    public async Task Invalid_Ir_Should_Be_Rejected_With_Reason()
    {
        // 映射后块缺少 blockId（graph 行无 block_uid）→ 内部适配 IR 校验拒收
        _anGineerClient.Package = _anGineerClient.Package with
        {
            GraphJsonl = Encoding.UTF8.GetBytes("{\"block_type\":\"paragraph\",\"page_idx\":0,\"plain_text\":\"缺 id\",\"bbox\":[0.1,0.1,0.9,0.2]}")
        };
        var (task, doc) = await CreateTaskWithBidDocAsync();

        await RunParseJobAsync(task.Id, doc.Id);

        var docRepo = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<CompareDocument, Guid>>();
        var failed = await docRepo.GetAsync(doc.Id);
        failed.ParseStatus.ShouldBe(DocumentParseStatus.Failed);
        failed.ParseError.ShouldContain("IrValidationFailed");
    }

    [Fact]
    public async Task Artifacts_With_Missing_Bbox_And_Table_Html_Should_Parse()
    {
        // v2 降级：AnGIneer 部分块暂无 bbox / table.html，校验器放行后应正常解析
        _anGineerClient.Package = _anGineerClient.Package with
        {
            GraphJsonl = Encoding.UTF8.GetBytes("""
                {"block_uid":"b0001","block_type":"title","page_idx":0,"plain_text":"t","derived_level":1,"source":"text","confidence":1.0}
                {"block_uid":"b0002","block_type":"table","page_idx":1,"plain_text":"p","derived_level":0,"image_path":"images/t1.jpg","source":"table","confidence":1.0}
                """)
        };
        var (task, doc) = await CreateTaskWithBidDocAsync();

        await RunParseJobAsync(task.Id, doc.Id);

        var detail = await _appService.GetAsync(task.Id);
        detail.Status.ShouldBe(CompareTaskStatus.Parsed);
        var docRepo = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<CompareDocument, Guid>>();
        (await docRepo.GetAsync(doc.Id)).ParseStatus.ShouldBe(DocumentParseStatus.Parsed);
    }

    [Fact]
    public async Task Long_Validation_Error_Should_Mark_Failed_Instead_Of_Crashing()
    {
        // 复现线上卡死：校验错误列表超过 2048 字符时，修复前 MarkParseFailed 抛异常，
        // 文档停留在 Parsing 且后台任务无限重试；现在应截断并落为 Failed。
        var jsonl = new StringBuilder();
        for (var i = 0; i < 100; i++)
        {
            jsonl.AppendLine(
                "{\"block_type\":\"paragraph\",\"page_idx\":0,\"plain_text\":\"x\",\"bbox\":[0.1,0.1,0.9,0.2],\"source\":\"text\",\"confidence\":1.0}");
        }
        _anGineerClient.Package = _anGineerClient.Package with
        {
            GraphJsonl = Encoding.UTF8.GetBytes(jsonl.ToString())
        };
        var (task, doc) = await CreateTaskWithBidDocAsync();

        await RunParseJobAsync(task.Id, doc.Id);

        var detail = await _appService.GetAsync(task.Id);
        detail.Status.ShouldBe(CompareTaskStatus.Failed);
        var docRepo = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<CompareDocument, Guid>>();
        var failed = await docRepo.GetAsync(doc.Id);
        failed.ParseStatus.ShouldBe(DocumentParseStatus.Failed);
        failed.ParseError.ShouldNotBeNull();
        failed.ParseError!.Length.ShouldBe(2048);
    }

    [Fact]
    public async Task GetDocumentIr_Should_Throw_When_Not_Parsed()
    {
        var (task, doc) = await CreateTaskWithBidDocAsync();

        var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(
            () => _appService.GetDocumentIrAsync(task.Id, doc.Id));
        ex.Code.ShouldBe(BidCompareErrorCodes.IrNotReady);
    }

    [Fact]
    public async Task Interrupted_Failure_Should_Auto_Resume_And_Recover()
    {
        // docs-api 重启会把解析中断任务标记 failed（提示可 resume）；轮询中应自动恢复一次，不应直接判失败。
        _anGineerClient.StateSequence = new ConcurrentQueue<AnGineerJobStatus>(new[]
        {
            new AnGineerJobStatus(AnGineerJobState.Failed, 100, "failed",
                "服务重启导致解析中断，可调用 /api/v1/documents/fake-job/resume 恢复"),
            new AnGineerJobStatus(AnGineerJobState.Succeeded, 100, "completed", "解析结束: completed")
        });
        var (task, doc) = await CreateTaskWithBidDocAsync();

        await RunParseJobAsync(task.Id, doc.Id);

        var detail = await _appService.GetAsync(task.Id);
        detail.Status.ShouldBe(CompareTaskStatus.Parsed);
        _anGineerClient.ResumeCount.ShouldBeGreaterThanOrEqualTo(1);
        _fileStorage.Objects.Keys.ShouldContain($"compare/{task.Id}/{doc.Id}/ir.json");
    }

    [Fact]
    public async Task AnGIneer_Failure_Should_Carry_Real_Reason_In_ParseError()
    {
        // 失败信息应透传 docs-api 的真实原因（如服务重启中断），而不是只剩文件名。
        _anGineerClient.FailWith = "服务重启导致解析中断，可调用 /api/v1/documents/fake-job/resume 恢复";
        var (task, doc) = await CreateTaskWithBidDocAsync();

        await RunParseJobAsync(task.Id, doc.Id);

        var docRepo = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<CompareDocument, Guid>>();
        var failed = await docRepo.GetAsync(doc.Id);
        failed.ParseStatus.ShouldBe(DocumentParseStatus.Failed);
        failed.ParseError.ShouldContain("服务重启导致解析中断");
    }

    [Fact]
    public async Task Stalled_Progress_Should_Resume_Once_Then_Fail_Fast()
    {
        // 复现线上卡死：raw_parse + 0% + 非空消息，progress/stage/message 长时间无变化。
        // 修复前会一直轮询到 30 分钟超时独占后台 worker；修复后 resume 一次仍无进展即 fail-fast。
        var pollOptions = GetRequiredService<IOptions<AnGineerPollOptions>>().Value;
        pollOptions.PollInterval = TimeSpan.FromMilliseconds(10);
        pollOptions.StallTimeout = TimeSpan.FromMilliseconds(100);
        pollOptions.Timeout = TimeSpan.FromSeconds(2); // 未实现时也能快速以“轮询超时”失败，避免等 30 分钟

        _anGineerClient.RepeatingState = new AnGineerJobStatus(
            AnGineerJobState.Processing, 0, "raw_parse", "label 归一化");
        var (task, doc) = await CreateTaskWithBidDocAsync();

        await RunParseJobAsync(task.Id, doc.Id);

        var docRepo = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<CompareDocument, Guid>>();
        var failed = await docRepo.GetAsync(doc.Id);
        failed.ParseStatus.ShouldBe(DocumentParseStatus.Failed);
        failed.ParseError.ShouldContain("停滞");
        _anGineerClient.ResumeCount.ShouldBe(1); // 每个停滞期最多 resume 一次

        var detail = await _appService.GetAsync(task.Id);
        detail.Status.ShouldBe(CompareTaskStatus.Failed);
    }

    [Fact]
    public async Task Progress_Change_Should_Reset_Stall_Timer()
    {
        // 总耗时超过 StallTimeout，但每次轮询 signature 都推进 → 不应被判停滞
        var pollOptions = GetRequiredService<IOptions<AnGineerPollOptions>>().Value;
        pollOptions.PollInterval = TimeSpan.FromMilliseconds(50);
        pollOptions.StallTimeout = TimeSpan.FromMilliseconds(100);

        _anGineerClient.StateSequence = new ConcurrentQueue<AnGineerJobStatus>(new[]
        {
            new AnGineerJobStatus(AnGineerJobState.Processing, 0, "raw_parse", "label 归一化"),
            new AnGineerJobStatus(AnGineerJobState.Processing, 0, "raw_parse", "label 归一化"),
            new AnGineerJobStatus(AnGineerJobState.Processing, 10, "raw_parse", "label 归一化"),
            new AnGineerJobStatus(AnGineerJobState.Processing, 20, "raw_parse", "label 归一化"),
            new AnGineerJobStatus(AnGineerJobState.Succeeded, 100, "completed", "解析结束: completed")
        });
        var (task, doc) = await CreateTaskWithBidDocAsync();

        await RunParseJobAsync(task.Id, doc.Id);

        var detail = await _appService.GetAsync(task.Id);
        detail.Status.ShouldBe(CompareTaskStatus.Parsed);
    }
}
