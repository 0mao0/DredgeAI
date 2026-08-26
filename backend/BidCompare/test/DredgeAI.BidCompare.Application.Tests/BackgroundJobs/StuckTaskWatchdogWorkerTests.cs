using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.EntityFrameworkCore;
using DredgeAI.BidCompare.TenderReadings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
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

    [Fact]
    public async Task Sweep_Should_Reenqueue_Extraction_For_Parsed_Stuck_TenderReadTask()
    {
        var appService = GetRequiredService<ITenderReadingAppService>();
        var created = await appService.CreateAsync(new CreateTenderReadingTaskDto { Name = "t" });
        var doc = await appService.UploadDocumentAsync(
            created.Id,
            "标书.pdf",
            new MemoryStream(Encoding.UTF8.GetBytes("%PDF fake")));

        var taskRepo = GetRequiredService<IRepository<TenderReadingTask, Guid>>();
        var docRepo = GetRequiredService<IRepository<TenderReadingDocument, Guid>>();
        await WithUnitOfWorkAsync(async () =>
        {
            var docEntity = await docRepo.GetAsync(doc.Id);
            docEntity.MarkParsed("tender-read/t/ir.json", null, 3);
            await docRepo.UpdateAsync(docEntity, autoSave: true);

            var taskEntity = await taskRepo.GetAsync(created.Id);
            taskEntity.StartParsing();
            taskEntity.MarkParsed();
            await taskRepo.UpdateAsync(taskEntity, autoSave: true);
        });

        // 回拨 LastModificationTime：模拟“解析落定后长时间未触发抽取（入队失败/Job 崩溃）”
        var dbContextProvider = GetRequiredService<IDbContextProvider<BidCompareDbContext>>();
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await dbContextProvider.GetDbContextAsync();
            await dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE BcTenderReadingTasks SET LastModificationTime = datetime('now', 'localtime', '-10 minutes') WHERE Id = {0}",
                created.Id);
        });

        _jobManager.Clear();
        var worker = GetRequiredService<StuckTaskWatchdogWorker>();
        await worker.SweepAsync(ServiceProvider, DateTime.UtcNow);

        var enqueued = _jobManager.LastEnqueued<ExtractBaselineArgs>();
        enqueued.ShouldNotBeNull();
        enqueued!.TaskId.ShouldBe(created.Id);

        var after = await taskRepo.GetAsync(created.Id);
        after.Status.ShouldBe(TenderReadingTaskStatus.Parsed);

        // 恢复后 LastModificationTime 已刷新：再次巡检不应重复入队（避免 LLM 重复计费）
        await worker.SweepAsync(ServiceProvider, DateTime.UtcNow);
        _jobManager.EnqueuedArgs.OfType<ExtractBaselineArgs>().Count(a => a.TaskId == created.Id).ShouldBe(1);
    }

    [Fact]
    public async Task Sweep_Should_Enqueue_Extraction_When_Worker_Was_Resolved_From_Disposed_Scope()
    {
        // 回归：周期性后台工作者由 ABP 在应用启动作用域创建，该作用域随后被释放。
        // 修复前构造函数注入的 IBackgroundJobManager 绑定到已释放作用域，EnqueueAsync 抛 ObjectDisposedException，
        // 导致 Parsed 卡住任务的抽取永远无法被看门狗补拉（生产日志每 5/15 分钟复现一次）。
        StuckTaskWatchdogWorker worker;
        using (var scope = ServiceProvider.CreateScope())
        {
            worker = scope.ServiceProvider.GetRequiredService<StuckTaskWatchdogWorker>();
        }
        // 生产环境：worker 在启动作用域创建后该作用域被释放，但 Logger 的懒加载仍可用；
        // 这里把懒加载重绑到根 provider，等价复现生产场景，只让构造函数捕获的 scoped 依赖失效。
        worker.LazyServiceProvider = new AbpLazyServiceProvider(ServiceProvider);
        worker.ServiceProvider = ServiceProvider;

        var appService = GetRequiredService<ITenderReadingAppService>();
        var created = await appService.CreateAsync(new CreateTenderReadingTaskDto { Name = "t" });
        var doc = await appService.UploadDocumentAsync(
            created.Id,
            "标书.pdf",
            new MemoryStream(Encoding.UTF8.GetBytes("%PDF fake")));

        var taskRepo = GetRequiredService<IRepository<TenderReadingTask, Guid>>();
        var docRepo = GetRequiredService<IRepository<TenderReadingDocument, Guid>>();
        await WithUnitOfWorkAsync(async () =>
        {
            var docEntity = await docRepo.GetAsync(doc.Id);
            docEntity.MarkParsed("tender-read/t/ir.json", null, 3);
            await docRepo.UpdateAsync(docEntity, autoSave: true);

            var taskEntity = await taskRepo.GetAsync(created.Id);
            taskEntity.StartParsing();
            taskEntity.MarkParsed();
            await taskRepo.UpdateAsync(taskEntity, autoSave: true);
        });

        // 回拨 LastModificationTime：模拟“解析落定后长时间未触发抽取”
        var dbContextProvider = GetRequiredService<IDbContextProvider<BidCompareDbContext>>();
        await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await dbContextProvider.GetDbContextAsync();
            await dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE BcTenderReadingTasks SET LastModificationTime = datetime('now', 'localtime', '-10 minutes') WHERE Id = {0}",
                created.Id);
        });

        _jobManager.Clear();
        await worker.SweepAsync(ServiceProvider, DateTime.UtcNow);

        var enqueued = _jobManager.LastEnqueued<ExtractBaselineArgs>();
        enqueued.ShouldNotBeNull();
        enqueued!.TaskId.ShouldBe(created.Id);
    }
}
