using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DredgeAI.BidCompare.BackgroundJobs;
using DredgeAI.BidCompare.Clauses;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Storage;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace DredgeAI.BidCompare.CompareTasks;

public class CompareTaskAppServiceTests : BidCompareApplicationTestBase<BidCompareApplicationTestModule>
{
    private readonly ICompareTaskAppService _appService;
    private readonly InMemoryFileStorage _fileStorage;
    private readonly RecordingBackgroundJobManager _jobManager;

    public CompareTaskAppServiceTests()
    {
        _appService = GetRequiredService<ICompareTaskAppService>();
        _fileStorage = (InMemoryFileStorage)GetRequiredService<IFileStorage>();
        _jobManager = (RecordingBackgroundJobManager)GetRequiredService<Volo.Abp.BackgroundJobs.IBackgroundJobManager>();
    }

    [Fact]
    public async Task Create_Then_Get_Should_Return_Spec_Fields()
    {
        var created = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "一期工程比标" });

        created.Id.ShouldNotBe(Guid.Empty);
        created.Name.ShouldBe("一期工程比标");
        created.Status.ShouldBe(CompareTaskStatus.Parsing);
        created.DocIds.ShouldBeEmpty();
        created.TenderDocId.ShouldBeNull();
        created.ClauseSnapshot.ShouldBeNull();
        created.Progress.Stage.ShouldBe("parsing");
        created.CreatedAt.ShouldBeGreaterThan(DateTime.MinValue);

        var fetched = await _appService.GetAsync(created.Id);
        fetched.Name.ShouldBe("一期工程比标");
    }

    [Fact]
    public async Task Create_With_Clauses_Should_Lock_Snapshot()
    {
        var created = await _appService.CreateAsync(new CreateCompareTaskDto
        {
            Name = "t",
            Clauses = new()
            {
                new ClauseInputDto { Text = "须提供 ISO9001 证书", Mandatory = true, Category = "资质" }
            }
        });

        created.ClauseSnapshot.ShouldNotBeNull();
        created.ClauseSnapshot!.Count.ShouldBe(1);
        created.ClauseSnapshot[0].ClauseId.ShouldNotBeNullOrWhiteSpace();
        created.ClauseSnapshot[0].Source.ShouldBe(Clauses.ClauseSource.Manual);
        created.ClauseSnapshot[0].Mandatory.ShouldBeTrue();
    }

    [Fact]
    public async Task GetList_Should_Page_And_Filter()
    {
        await _appService.CreateAsync(new CreateCompareTaskDto { Name = "道路项目" });
        await _appService.CreateAsync(new CreateCompareTaskDto { Name = "桥梁项目" });

        var all = await _appService.GetListAsync(new GetCompareTasksInput { MaxResultCount = 10 });
        all.TotalCount.ShouldBe(2);
        all.Items.Count.ShouldBe(2);

        var filtered = await _appService.GetListAsync(new GetCompareTasksInput { Name = "道路", MaxResultCount = 10 });
        filtered.TotalCount.ShouldBe(1);
        filtered.Items[0].Name.ShouldBe("道路项目");
    }

    [Fact]
    public async Task UploadDocument_Should_Store_File_And_Enqueue_Parse()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var content = new MemoryStream(Encoding.UTF8.GetBytes("%PDF fake"));

        var doc = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf", content);

        doc.TaskId.ShouldBe(task.Id);
        doc.Role.ShouldBe(DocumentRole.Bid);
        doc.ParseStatus.ShouldBe(DocumentParseStatus.Pending);
        _fileStorage.Objects.Keys.ShouldContain(k => k.StartsWith($"compare/{task.Id}/{doc.Id}/origin"));

        var enqueued = _jobManager.LastEnqueued<ParseDocumentArgs>();
        enqueued.ShouldNotBeNull();
        enqueued!.TaskId.ShouldBe(task.Id);
        enqueued.DocumentId.ShouldBe(doc.Id);

        var detail = await _appService.GetAsync(task.Id);
        detail.DocIds.ShouldContain(doc.Id);
    }

    [Fact]
    public async Task UploadDocument_Should_Reject_Unsupported_Extension()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "名单.xlsx",
                new MemoryStream(new byte[] { 1 })));
        ex.Code.ShouldBe(BidCompareErrorCodes.UnsupportedFileType);
    }

    [Fact]
    public async Task UploadDocument_Should_Enforce_Max_5_Bid_Documents()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        for (var i = 0; i < 5; i++)
        {
            await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, $"标书{i}.pdf",
                new MemoryStream(new byte[] { 1 }));
        }

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "第6份.pdf",
                new MemoryStream(new byte[] { 1 })));
        ex.Code.ShouldBe(BidCompareErrorCodes.DocumentCountOutOfRange);
    }

    [Fact]
    public async Task Upload_Tender_Document_Should_Set_TenderDocId()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var doc = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Tender, "招标文件.pdf",
            new MemoryStream(new byte[] { 1 }));

        var detail = await _appService.GetAsync(task.Id);
        detail.TenderDocId.ShouldBe(doc.Id);
    }

    [Fact]
    public async Task Delete_Should_Remove_Task_And_Storage_Objects()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var doc = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf",
            new MemoryStream(new byte[] { 1 }));

        await _appService.DeleteAsync(task.Id);

        var repo = GetRequiredService<IRepository<CompareTask, Guid>>();
        (await repo.FindAsync(task.Id)).ShouldBeNull();
        _fileStorage.Objects.Keys.Any(k => k.Contains(doc.Id.ToString())).ShouldBeFalse();
    }
}
