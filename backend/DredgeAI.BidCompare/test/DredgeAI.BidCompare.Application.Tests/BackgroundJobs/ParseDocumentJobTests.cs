using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.AnGineer;
using DredgeAI.BidCompare.Storage;
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

        // IR API 可读取（spec §6 GET ir；内容为内部适配形态）
        var ir = await _appService.GetDocumentIrAsync(task.Id, doc.Id);
        ir.DocId.ShouldBe(doc.Id.ToString()); // 内部适配 IR 的 docId 为本系统文档 id
        ir.Meta.FileName.ShouldBe("标书A.pdf");
        ir.Blocks.Count.ShouldBe(3);
        ir.Blocks[1].Table.ShouldNotBeNull();
        ir.Blocks[0].Bbox.ShouldBe(new double[] { 0.0672, 0.0594, 0.9244, 0.095 }); // 0~1 归一化（v2 §2）

        // 第二份标书解析完成 → 自动进入比对
        var docB = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书B.pdf",
            new MemoryStream(new byte[] { 2 }));
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
            new MemoryStream(new byte[] { 1 }));
        var bid = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf",
            new MemoryStream(new byte[] { 1 }));

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
    public async Task Partial_Failure_With_Less_Than_Two_Bids_Should_Wait_For_Reparse()
    {
        // v2 §5.4：失败导致可用标书 < 2 份时，在两两对比之前停留并提示重新解析失败文档
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var good = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf",
            new MemoryStream(new byte[] { 1 }));
        var bad = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书B.pdf",
            new MemoryStream(new byte[] { 1 }));

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
}
