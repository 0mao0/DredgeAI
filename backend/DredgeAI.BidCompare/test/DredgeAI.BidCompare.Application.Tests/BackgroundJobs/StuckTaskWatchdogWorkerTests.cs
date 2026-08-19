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

public class StuckTaskWatchdogWorkerTests : BidCompareApplicationTestBase<BidCompareApplicationTestModule>
{
    private readonly ICompareTaskAppService _appService;
    private readonly RecordingBackgroundJobManager _jobManager;

    public StuckTaskWatchdogWorkerTests()
    {
        _appService = GetRequiredService<ICompareTaskAppService>();
        _jobManager = (RecordingBackgroundJobManager)GetRequiredService<IBackgroundJobManager>();
    }

    [Fact]
    public async Task Sweep_Should_Not_Treat_Recent_Utc_ParseStartedAt_As_Stuck()
    {
        // ParseStartedAt 由 MarkParsing 写 DateTime.UtcNow（UTC 朴素值）；
        // 修复前看门狗用本地时间（_clock.Now）比较，刚启动 1 分钟的解析会被误判为“已超时 35 分钟”并重新入队。
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var doc = await _appService.UploadDocumentAsync(
            task.Id, DocumentRole.Bid, "标书D.pdf",
            new MemoryStream(Encoding.UTF8.GetBytes("%PDF fake")));
        var docRepo = GetRequiredService<IRepository<CompareDocument, Guid>>();
        await WithUnitOfWorkAsync(async () =>
        {
            var entity = await docRepo.GetAsync(doc.Id);
            entity.MarkParsing();
            entity.SetAnGineerDocId("recent-angineer-doc");
            await docRepo.UpdateAsync(entity, autoSave: true);
        });
        var startedAtUtc = (await docRepo.GetAsync(doc.Id)).ParseStartedAt!.Value;
        _jobManager.Clear();

        var worker = GetRequiredService<StuckTaskWatchdogWorker>();
        await worker.SweepAsync(ServiceProvider, startedAtUtc.AddMinutes(1));

        var after = await docRepo.GetAsync(doc.Id);
        after.ParseStatus.ShouldBe(DocumentParseStatus.Parsing);
        _jobManager.LastEnqueued<ParseDocumentArgs>().ShouldBeNull();
    }
}
