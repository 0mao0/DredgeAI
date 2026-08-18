using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace DredgeAI.BidCompare.BackgroundJobs;

/// <summary>
/// 进程重启恢复：把仍处于 Parsing 且已有 AnGIneer doc_id 的文档重新入队续跑。
/// ParseDocumentJob 会先查 AnGIneer 状态，processing/failed 时调 resume，避免重新上传文件。
/// 仅恢复“近期”启动的解析（ParseStartedAt 在 DocumentParsingTimeout 内）；
/// 长期停滞的文档直接标记失败，避免重启反复复活同一卡死任务（2026-08-18 事故）。
/// </summary>
public class ParseRecoveryService : ITransientDependency
{
    private readonly IRepository<CompareDocument, Guid> _documentRepository;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly WatchdogOptions _watchdogOptions;
    private readonly ILogger<ParseRecoveryService> _logger;

    public ParseRecoveryService(
        IRepository<CompareDocument, Guid> documentRepository,
        IBackgroundJobManager backgroundJobManager,
        IOptions<WatchdogOptions> watchdogOptions,
        ILogger<ParseRecoveryService> logger)
    {
        _documentRepository = documentRepository;
        _backgroundJobManager = backgroundJobManager;
        _watchdogOptions = watchdogOptions.Value;
        _logger = logger;
    }

    public Task RecoverAsync(CancellationToken cancellationToken = default)
        => RecoverAsync(DateTime.UtcNow, cancellationToken);

    /// <summary>now 参数供测试注入固定时间点。</summary>
    public async Task RecoverAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var documents = await _documentRepository.GetListAsync(
            d => d.ParseStatus == DocumentParseStatus.Parsing && d.AnGineerDocId != null,
            cancellationToken: cancellationToken);
        var targets = documents
            .Where(d => !string.IsNullOrWhiteSpace(d.AnGineerDocId))
            .ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var deadline = now - _watchdogOptions.DocumentParsingTimeout;
        var hopeless = targets
            .Where(d => d.ParseStartedAt == null || d.ParseStartedAt.Value < deadline)
            .ToList();
        foreach (var document in hopeless)
        {
            var reason =
                $"启动恢复：解析自 {document.ParseStartedAt:O} 起已超过 {_watchdogOptions.DocumentParsingTimeout.TotalMinutes} 分钟仍无终态，按停滞处理";
            _logger.LogWarning("文档 {DocumentId} {Reason}，直接标记失败（不再入队）", document.Id, reason);
            document.MarkParseFailed(reason);
            await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
        }

        var resumable = targets
            .Where(d => d.ParseStartedAt != null && d.ParseStartedAt.Value >= deadline)
            .ToList();
        if (resumable.Count == 0)
        {
            return;
        }

        _logger.LogInformation(
            "启动恢复：发现 {Count} 个解析中且已有 AnGIneer doc_id 的近期文档，重新入队续跑",
            resumable.Count);
        foreach (var document in resumable)
        {
            await _backgroundJobManager.EnqueueAsync(new ParseDocumentArgs
            {
                TaskId = document.TaskId,
                DocumentId = document.Id
            });
        }
    }
}
