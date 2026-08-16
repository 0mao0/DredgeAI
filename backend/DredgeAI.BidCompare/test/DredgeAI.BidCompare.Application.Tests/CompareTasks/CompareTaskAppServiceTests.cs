using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Analysis;
using DredgeAI.BidCompare.AnGineer;
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
        created.NameEditedByUser.ShouldBeFalse();
        created.SuggestedName.ShouldBeNull();
        created.Pairs.ShouldBeNull();
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
    public async Task UpdateName_Should_Set_Edited_Flag()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "初稿" });

        var updated = await _appService.UpdateNameAsync(task.Id, new UpdateCompareTaskNameInput { Name = "一期工程比标" });

        updated.Name.ShouldBe("一期工程比标");
        updated.NameEditedByUser.ShouldBeTrue();

        var fetched = await _appService.GetAsync(task.Id);
        fetched.Name.ShouldBe("一期工程比标");
        fetched.NameEditedByUser.ShouldBeTrue();
    }

    [Fact]
    public async Task Reparse_Should_Only_Reprocess_Requested_Failed_Docs()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var good = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf",
            TestFiles.Pdf(10));
        var bad = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书B.pdf",
            TestFiles.Pdf(11));
        var anGineer = (FakeAnGineerClient)GetRequiredService<IAnGineerClient>();
        anGineer.FailWith = "OCR 崩溃";
        await WithUnitOfWorkAsync(async () =>
        {
            await GetRequiredService<ParseDocumentJob>().ExecuteAsync(
                new ParseDocumentArgs { TaskId = task.Id, DocumentId = bad.Id });
        });
        anGineer.FailWith = null;
        await WithUnitOfWorkAsync(async () =>
        {
            await GetRequiredService<ParseDocumentJob>().ExecuteAsync(
                new ParseDocumentArgs { TaskId = task.Id, DocumentId = good.Id });
        });

        _jobManager.Clear();
        var reparse = await _appService.ReparseAsync(task.Id,
            new ReparseDocumentsInput { DocIds = new() { bad.Id } });

        reparse.Status.ShouldBe(CompareTaskStatus.Parsing);
        var docRepo = GetRequiredService<IRepository<CompareDocument, Guid>>();
        (await docRepo.GetAsync(bad.Id)).ParseStatus.ShouldBe(DocumentParseStatus.Pending);
        (await docRepo.GetAsync(good.Id)).ParseStatus.ShouldBe(DocumentParseStatus.Parsed);
        var enqueued = _jobManager.LastEnqueued<ParseDocumentArgs>();
        enqueued.ShouldNotBeNull();
        enqueued!.DocumentId.ShouldBe(bad.Id);

        // 仅允许失败文档重新解析
        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _appService.ReparseAsync(task.Id, new ReparseDocumentsInput { DocIds = new() { good.Id } }));
        ex.Code.ShouldBe(BidCompareErrorCodes.InvalidTaskState);
    }

    [Fact]
    public async Task RetryCompare_Should_Reject_While_Analyzing()
    {
        var (taskId, _, _) = await PrepareParsedBidsAsync();
        var taskRepo = GetRequiredService<IRepository<CompareTask, Guid>>();
        await WithUnitOfWorkAsync(async () =>
        {
            var task = await taskRepo.GetAsync(taskId);
            task.MarkAnalyzing();
            await taskRepo.UpdateAsync(task, autoSave: true);
        });

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _appService.RetryCompareAsync(taskId, new RetryCompareInput()));
        ex.Code.ShouldBe(BidCompareErrorCodes.InvalidTaskState);
    }

    [Fact]
    public async Task RetryCompare_Should_Enqueue_After_Failure()
    {
        var (taskId, _, _) = await PrepareParsedBidsAsync();
        var algo = (FakeCompareAlgoClient)GetRequiredService<ICompareAlgoClient>();
        algo.FailWith = "connection refused";
        await WithUnitOfWorkAsync(async () =>
        {
            await GetRequiredService<CompareDocumentsJob>().ExecuteAsync(
                new CompareDocumentsArgs { TaskId = taskId });
        });
        var failed = await _appService.GetAsync(taskId);
        failed.Status.ShouldBe(CompareTaskStatus.Failed);

        algo.FailWith = null;
        _jobManager.Clear();
        var retried = await _appService.RetryCompareAsync(taskId, new RetryCompareInput());

        retried.Status.ShouldBe(CompareTaskStatus.Comparing);
        _jobManager.LastEnqueued<CompareDocumentsArgs>().ShouldNotBeNull();
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
    public async Task UploadDocument_Should_Store_File_Without_Enqueue()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var content = new MemoryStream(Encoding.UTF8.GetBytes("%PDF fake"));

        var doc = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf", content);

        doc.TaskId.ShouldBe(task.Id);
        doc.Role.ShouldBe(DocumentRole.Bid);
        doc.ParseStatus.ShouldBe(DocumentParseStatus.Pending);
        _fileStorage.Objects.Keys.ShouldContain(k => k.StartsWith($"compare/{task.Id}/{doc.Id}/origin"));

        // v2 修订：上传不再逐份入队，改为全部上传后由 StartParsing 批量并发解析
        _jobManager.LastEnqueued<ParseDocumentArgs>().ShouldBeNull();

        var detail = await _appService.GetAsync(task.Id);
        detail.DocIds.ShouldContain(doc.Id);
    }

    [Fact]
    public async Task StartParsing_Should_Enqueue_Batch_For_Pending_Documents()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var docA = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "a.pdf",
            TestFiles.Pdf(10));
        var docB = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "b.pdf",
            TestFiles.Pdf(11));
        _jobManager.Clear();

        var dto = await _appService.StartParsingAsync(task.Id);

        var enqueued = _jobManager.LastEnqueued<ParseDocumentsArgs>();
        enqueued.ShouldNotBeNull();
        enqueued!.TaskId.ShouldBe(task.Id);
        enqueued.DocumentIds.ShouldContain(docA.Id);
        enqueued.DocumentIds.ShouldContain(docB.Id);
        dto.Status.ShouldBe(CompareTaskStatus.Parsing);
    }

    [Fact]
    public async Task StartParsing_With_No_Pending_Should_Not_Enqueue()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        _jobManager.Clear();

        var dto = await _appService.StartParsingAsync(task.Id);

        _jobManager.LastEnqueued<ParseDocumentsArgs>().ShouldBeNull();
        dto.Status.ShouldBe(CompareTaskStatus.Parsing);
    }

    [Fact]
    public async Task GetDocumentFile_Should_Return_Original_Stream_And_Name()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var bytes = Encoding.UTF8.GetBytes("%PDF-1.4 fake");
        var doc = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf",
            new MemoryStream(bytes));

        var result = await _appService.GetDocumentFileAsync(task.Id, doc.Id);

        result.FileName.ShouldBe("标书A.pdf");
        result.ContentType.ShouldBe("application/pdf");
        using var reader = new MemoryStream();
        await result.Content.CopyToAsync(reader);
        reader.ToArray().ShouldBe(bytes);
    }

    [Fact]
    public async Task GetDocumentFile_Should_Reject_Doc_From_Another_Task()
    {
        var taskA = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "A" });
        var taskB = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "B" });
        var doc = await _appService.UploadDocumentAsync(taskA.Id, DocumentRole.Bid, "标书A.pdf",
            TestFiles.Pdf(1));

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _appService.GetDocumentFileAsync(taskB.Id, doc.Id));
        ex.Code.ShouldBe(BidCompareErrorCodes.DocumentNotFound);
    }

    [Fact]
    public async Task GetDocuments_Should_Return_Task_Documents_In_Upload_Order()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var docA = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf", TestFiles.Pdf(10));
        var docB = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书B.pdf", TestFiles.Pdf(11));

        var documents = await _appService.GetDocumentsAsync(task.Id);

        documents.Count.ShouldBe(2);
        documents.Select(d => d.Id).ShouldBe(new[] { docA.Id, docB.Id });
        documents[0].FileName.ShouldBe("标书A.pdf");
        documents[0].Role.ShouldBe(DocumentRole.Bid);
        documents[0].ParseStatus.ShouldBe(DocumentParseStatus.Pending);
        documents[0].FileSize.ShouldBe(5); // "%PDF" 头 + 1 字节标记
    }

    [Fact]
    public async Task UploadDocument_Should_Reject_Unsupported_Extension()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "名单.xlsx",
                TestFiles.Pdf(1)));
        ex.Code.ShouldBe(BidCompareErrorCodes.UnsupportedFileType);
    }

    [Fact]
    public async Task UploadDocument_Should_Enforce_Max_8_Bid_Documents()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        for (var i = 0; i < 8; i++)
        {
            await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, $"标书{i}.pdf",
                TestFiles.Pdf(1));
        }

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "第9份.pdf",
                TestFiles.Pdf(1)));
        ex.Code.ShouldBe(BidCompareErrorCodes.DocumentCountOutOfRange);
    }

    [Fact]
    public async Task Upload_Tender_Document_Should_Set_TenderDocId()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var doc = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Tender, "招标文件.pdf",
            TestFiles.Pdf(1));

        var detail = await _appService.GetAsync(task.Id);
        detail.TenderDocId.ShouldBe(doc.Id);
    }

    [Fact]
    public async Task Delete_Should_Remove_Task_And_Storage_Objects()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var doc = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf",
            TestFiles.Pdf(1));

        await _appService.DeleteAsync(task.Id);

        var repo = GetRequiredService<IRepository<CompareTask, Guid>>();
        (await repo.FindAsync(task.Id)).ShouldBeNull();
        _fileStorage.Objects.Keys.Any(k => k.Contains(doc.Id.ToString())).ShouldBeFalse();
    }

    private async Task<(Guid TaskId, Guid DocA, Guid DocB)> PrepareParsedBidsAsync()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var docA = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf",
            TestFiles.Pdf(10));
        var docB = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书B.pdf",
            TestFiles.Pdf(11));
        var parseJob = GetRequiredService<ParseDocumentJob>();
        await WithUnitOfWorkAsync(async () =>
        {
            await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = docA.Id });
            await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = docB.Id });
        });
        return (task.Id, docA.Id, docB.Id);
    }
}
