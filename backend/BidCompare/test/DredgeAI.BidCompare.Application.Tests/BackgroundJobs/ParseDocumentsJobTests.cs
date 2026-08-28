using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AnGineer;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Xunit;

namespace DredgeAI.BidCompare.BackgroundJobs;

public class ParseDocumentsJobTests : BidCompareApplicationTestBase<BidCompareApplicationTestModule>
{
    private readonly ICompareTaskAppService _appService;
    private readonly RecordingBackgroundJobManager _jobManager;
    private readonly FakeAnGineerClient _anGineerClient;

    public ParseDocumentsJobTests()
    {
        _appService = GetRequiredService<ICompareTaskAppService>();
        _jobManager = (RecordingBackgroundJobManager)GetRequiredService<IBackgroundJobManager>();
        _anGineerClient = (FakeAnGineerClient)GetRequiredService<IAnGineerClient>();
    }

    private async Task<Guid> UploadBidAsync(Guid taskId, string fileName)
    {
        var doc = await _appService.UploadDocumentAsync(taskId, DocumentRole.Bid, fileName,
            new MemoryStream(Encoding.UTF8.GetBytes("%PDF fake")));
        return doc.Id;
    }

    private async Task RunBatchJobAsync(Guid taskId, params Guid[] documentIds)
    {
        var job = GetRequiredService<ParseDocumentsJob>();
        await WithUnitOfWorkAsync(async () =>
        {
            await job.ExecuteAsync(new ParseDocumentsArgs
            {
                TaskId = taskId,
                DocumentIds = documentIds.ToList()
            });
        });
    }

    [Fact]
    public async Task Batch_Parse_Should_Submit_Concurrently_And_Advance_Task()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var docA = await UploadBidAsync(task.Id, "A.pdf");
        var docB = await UploadBidAsync(task.Id, "B.pdf");
        _anGineerClient.SubmitDelayMs = 300;

        await RunBatchJobAsync(task.Id, docA, docB);

        _anGineerClient.MaxConcurrentSubmits.ShouldBeGreaterThanOrEqualTo(2);
        var docRepo = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<CompareDocument, Guid>>();
        (await docRepo.GetAsync(docA)).ParseStatus.ShouldBe(DocumentParseStatus.Parsed);
        (await docRepo.GetAsync(docB)).ParseStatus.ShouldBe(DocumentParseStatus.Parsed);
        var detail = await _appService.GetAsync(task.Id);
        detail.Status.ShouldBe(CompareTaskStatus.Comparing);
        _jobManager.LastEnqueued<CompareDocumentsArgs>().ShouldNotBeNull();
    }

    [Fact]
    public async Task Batch_Parse_With_One_Failure_Should_Mark_Partial_And_Keep_Others()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var docA = await UploadBidAsync(task.Id, "A.pdf");
        var docB = await UploadBidAsync(task.Id, "B.pdf");
        _anGineerClient.FailFileNames.Add("B.pdf");

        await RunBatchJobAsync(task.Id, docA, docB);

        var docRepo = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<CompareDocument, Guid>>();
        (await docRepo.GetAsync(docA)).ParseStatus.ShouldBe(DocumentParseStatus.Parsed);
        var failed = await docRepo.GetAsync(docB);
        failed.ParseStatus.ShouldBe(DocumentParseStatus.Failed);
        failed.ParseError.ShouldContain("B.pdf");
        var detail = await _appService.GetAsync(task.Id);
        detail.Status.ShouldBe(CompareTaskStatus.Partial);
        detail.FailureReason.ShouldContain("B.pdf");
    }

    [Fact]
    public async Task Batch_Parse_All_Failed_Should_Mark_Task_Failed()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var docA = await UploadBidAsync(task.Id, "A.pdf");
        var docB = await UploadBidAsync(task.Id, "B.pdf");
        _anGineerClient.FailFileNames.Add("A.pdf");
        _anGineerClient.FailFileNames.Add("B.pdf");

        await RunBatchJobAsync(task.Id, docA, docB);

        var detail = await _appService.GetAsync(task.Id);
        detail.Status.ShouldBe(CompareTaskStatus.Failed);
        var docRepo = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<CompareDocument, Guid>>();
        (await docRepo.GetAsync(docA)).ParseStatus.ShouldBe(DocumentParseStatus.Failed);
        (await docRepo.GetAsync(docB)).ParseStatus.ShouldBe(DocumentParseStatus.Failed);
    }
}
