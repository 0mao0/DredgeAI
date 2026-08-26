using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DredgeAI.BidCompare.BackgroundJobs;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Exports;
using DredgeAI.BidCompare.Storage;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace DredgeAI.BidCompare.Exports;

public class ExportJobTests : BidCompareApplicationTestBase<BidCompareApplicationTestModule>
{
    private readonly ICompareTaskAppService _appService;
    private readonly InMemoryFileStorage _fileStorage;

    public ExportJobTests()
    {
        _appService = GetRequiredService<ICompareTaskAppService>();
        _fileStorage = (InMemoryFileStorage)GetRequiredService<IFileStorage>();
    }

    private async Task<Guid> PrepareDoneTaskAsync()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "一期比标" });
        var docA = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf", TestFiles.Pdf(1));
        var docB = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书B.pdf", TestFiles.Pdf(2));
        // 每个 Job 独立工作单元（与生产 BackgroundJobExecuter 每次执行一个 scope 一致）；
        // Job 内部会用独立工作单元做并发安全的状态推进，与外层共享 UoW 的实体跟踪会冲突
        var parseJob = GetRequiredService<ParseDocumentJob>();
        await WithUnitOfWorkAsync(async () =>
        {
            await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = docA.Id });
        });
        await WithUnitOfWorkAsync(async () =>
        {
            await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = docB.Id });
        });
        await WithUnitOfWorkAsync(async () =>
        {
            await GetRequiredService<CompareDocumentsJob>().ExecuteAsync(new CompareDocumentsArgs { TaskId = task.Id });
        });
        await WithUnitOfWorkAsync(async () =>
        {
            await GetRequiredService<AiAnalysisJob>().ExecuteAsync(new AiAnalysisArgs { TaskId = task.Id });
        });
        return task.Id;
    }

    private async Task RunExportJobAsync(Guid jobId)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await GetRequiredService<ExportReportJob>().ExecuteAsync(new ExportReportArgs { ExportJobId = jobId });
        });
    }

    [Fact]
    public async Task Word_Export_Should_Succeed_And_Return_DownloadUrl()
    {
        var taskId = await PrepareDoneTaskAsync();

        var handle = await _appService.RequestExportAsync(taskId, new ExportRequestDto { Format = ExportFormat.Word });
        handle.Status.ShouldBe(ExportJobStatus.Pending);
        handle.DownloadUrl.ShouldBeNull();

        await RunExportJobAsync(handle.JobId);

        var result = await _appService.GetExportJobAsync(taskId, handle.JobId);
        result.Status.ShouldBe(ExportJobStatus.Succeeded);
        result.DownloadUrl.ShouldNotBeNullOrWhiteSpace(); // spec §6.2：轮询获取下载链接
        _fileStorage.Objects.Keys.ShouldContain(k => k.StartsWith($"compare/{taskId}/exports/{handle.JobId}") && k.EndsWith(".docx"));
    }

    [Fact]
    public async Task Pdf_Export_Should_Convert_Via_PdfConverter()
    {
        var taskId = await PrepareDoneTaskAsync();
        var handle = await _appService.RequestExportAsync(taskId, new ExportRequestDto { Format = ExportFormat.Pdf });

        await RunExportJobAsync(handle.JobId);

        var result = await _appService.GetExportJobAsync(taskId, handle.JobId);
        result.Status.ShouldBe(ExportJobStatus.Succeeded);
        var key = _fileStorage.Objects.Keys.Single(k => k.Contains(handle.JobId.ToString()));
        key.ShouldEndWith(".pdf");
    }

    [Fact]
    public async Task Export_Before_Done_Should_Throw()
    {
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _appService.RequestExportAsync(task.Id, new ExportRequestDto { Format = ExportFormat.Word }));
        ex.Code.ShouldBe(BidCompareErrorCodes.ReportNotReady);
    }

    [Fact]
    public async Task GetExportJob_Of_Other_Task_Should_Throw()
    {
        var taskId = await PrepareDoneTaskAsync();
        var handle = await _appService.RequestExportAsync(taskId, new ExportRequestDto { Format = ExportFormat.Word });
        var otherTask = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "other" });

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _appService.GetExportJobAsync(otherTask.Id, handle.JobId));
        ex.Code.ShouldBe(BidCompareErrorCodes.ExportJobNotFound);
    }
}
