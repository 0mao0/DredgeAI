using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace DredgeAI.BidCompare.BackgroundJobs;

public class ParseRecoveryServiceTests : BidCompareApplicationTestBase<BidCompareApplicationTestModule>
{
    private readonly ICompareTaskAppService _appService;
    private readonly RecordingBackgroundJobManager _jobManager;

    public ParseRecoveryServiceTests()
    {
        _appService = GetRequiredService<ICompareTaskAppService>();
        _jobManager = (RecordingBackgroundJobManager)GetRequiredService<IBackgroundJobManager>();
    }

    [Fact]
    public async Task Recover_Should_Requeue_Parsing_Docs_With_AnGineer_DocId()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var doc = await _appService.UploadDocumentAsync(
            task.Id, DocumentRole.Bid, "标书A.pdf",
            new MemoryStream(Encoding.UTF8.GetBytes("%PDF fake")));
        var docRepo = GetRequiredService<IRepository<CompareDocument, Guid>>();
        await WithUnitOfWorkAsync(async () =>
        {
            var entity = await docRepo.GetAsync(doc.Id);
            entity.MarkParsing();
            entity.SetAnGineerDocId("existing-angineer-doc");
            await docRepo.UpdateAsync(entity, autoSave: true);
        });
        _jobManager.Clear();

        var service = GetRequiredService<ParseRecoveryService>();
        await service.RecoverAsync();

        var enqueued = _jobManager.LastEnqueued<ParseDocumentArgs>();
        enqueued.ShouldNotBeNull();
        enqueued!.TaskId.ShouldBe(task.Id);
        enqueued.DocumentId.ShouldBe(doc.Id);
    }

    [Fact]
    public async Task Recover_Should_Skip_Failed_Docs()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var doc = await _appService.UploadDocumentAsync(
            task.Id, DocumentRole.Bid, "标书B.pdf",
            new MemoryStream(Encoding.UTF8.GetBytes("%PDF fake")));
        var docRepo = GetRequiredService<IRepository<CompareDocument, Guid>>();
        await WithUnitOfWorkAsync(async () =>
        {
            var entity = await docRepo.GetAsync(doc.Id);
            entity.MarkParseFailed("解析失败");
            entity.SetAnGineerDocId("existing-angineer-doc");
            await docRepo.UpdateAsync(entity, autoSave: true);
        });
        _jobManager.Clear();

        var service = GetRequiredService<ParseRecoveryService>();
        await service.RecoverAsync();

        _jobManager.LastEnqueued<ParseDocumentArgs>().ShouldBeNull();
    }
}
