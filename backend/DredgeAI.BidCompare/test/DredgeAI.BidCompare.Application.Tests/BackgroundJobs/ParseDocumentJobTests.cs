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
        detail.Status.ShouldBe(CompareTaskStatus.Comparing); // 无招标文件 → 直接进入比对
        _fileStorage.Objects.Keys.ShouldContain($"compare/{task.Id}/{doc.Id}/ir.json"); // 内部适配 IR（v2 映射后）
        _fileStorage.Objects.Keys.ShouldContain($"compare/{task.Id}/{doc.Id}/content.md");
        _fileStorage.Objects.Keys.ShouldContain($"compare/{task.Id}/{doc.Id}/images/t1.jpg");
        _fileStorage.Objects.Keys.ShouldContain($"compare/{task.Id}/{doc.Id}/raw/doc_blocks_graph.jsonl"); // AnGIneer 原始产物留档
        _jobManager.LastEnqueued<CompareDocumentsArgs>().ShouldNotBeNull();

        // IR API 可读取（spec §6 GET ir；内容为内部适配形态）
        var ir = await _appService.GetDocumentIrAsync(task.Id, doc.Id);
        ir.DocId.ShouldBe(doc.Id.ToString()); // 内部适配 IR 的 docId 为本系统文档 id
        ir.Meta.FileName.ShouldBe("标书A.pdf");
        ir.Blocks.Count.ShouldBe(3);
        ir.Blocks[1].Table.ShouldNotBeNull();
        ir.Blocks[0].Bbox.ShouldBe(new double[] { 0.0672, 0.0594, 0.9244, 0.095 }); // 0~1 归一化（v2 §2）
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
    public async Task Partial_Failure_Should_Mark_Partial_But_Continue()
    {
        // spec §9：单份解析失败 → 部分完成，其余文档照常对比
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var good = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf",
            new MemoryStream(new byte[] { 1 }));
        var bad = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书B.pdf",
            new MemoryStream(new byte[] { 1 }));

        await RunParseJobAsync(task.Id, good.Id);

        _anGineerClient.FailWith = "OCR 崩溃";
        await RunParseJobAsync(task.Id, bad.Id);

        var detail = await _appService.GetAsync(task.Id);
        detail.Status.ShouldBe(CompareTaskStatus.Comparing); // Partial 为中间标记态，继续流转
        _jobManager.LastEnqueued<CompareDocumentsArgs>().ShouldNotBeNull();
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
    public async Task GetDocumentIr_Should_Throw_When_Not_Parsed()
    {
        var (task, doc) = await CreateTaskWithBidDocAsync();

        var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(
            () => _appService.GetDocumentIrAsync(task.Id, doc.Id));
        ex.Code.ShouldBe(BidCompareErrorCodes.IrNotReady);
    }
}
