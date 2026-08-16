using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Documents;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace DredgeAI.BidCompare.BackgroundJobs;

/// <summary>
/// 进程重启恢复：把仍处于 Parsing 且已有 AnGIneer doc_id 的文档重新入队。
/// ParseDocumentJob 会先查 AnGIneer 状态，processing/failed 时调 resume，避免重新上传文件。
/// </summary>
public class ParseRecoveryService : ITransientDependency
{
    private readonly IRepository<CompareDocument, Guid> _documentRepository;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILogger<ParseRecoveryService> _logger;

    public ParseRecoveryService(
        IRepository<CompareDocument, Guid> documentRepository,
        IBackgroundJobManager backgroundJobManager,
        ILogger<ParseRecoveryService> logger)
    {
        _documentRepository = documentRepository;
        _backgroundJobManager = backgroundJobManager;
        _logger = logger;
    }

    public async Task RecoverAsync(CancellationToken cancellationToken = default)
    {
        var documents = await _documentRepository.GetListAsync(
            d => d.ParseStatus == DocumentParseStatus.Parsing && d.AnGineerDocId != null,
            cancellationToken: cancellationToken);
        var targets = documents.Where(d => !string.IsNullOrWhiteSpace(d.AnGineerDocId)).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        _logger.LogInformation(
            "启动恢复：发现 {Count} 个解析中且已有 AnGIneer doc_id 的文档，重新入队续跑",
            targets.Count);
        foreach (var document in targets)
        {
            await _backgroundJobManager.EnqueueAsync(new ParseDocumentArgs
            {
                TaskId = document.TaskId,
                DocumentId = document.Id
            });
        }
    }
}
