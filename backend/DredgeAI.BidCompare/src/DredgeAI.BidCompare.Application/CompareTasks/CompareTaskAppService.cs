using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using DredgeAI.BidCompare.Analysis;
using DredgeAI.BidCompare.BackgroundJobs;
using DredgeAI.BidCompare.Clauses;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Evidences;
using DredgeAI.BidCompare.Exports;
using DredgeAI.BidCompare.Ir;
using DredgeAI.BidCompare.Reports;
using DredgeAI.BidCompare.Reporting;
using DredgeAI.BidCompare.Storage;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Validation;

namespace DredgeAI.BidCompare.CompareTasks;

[RemoteService(false)] // 精确路由由 HttpApi 显式 Controller 暴露
public class CompareTaskAppService : ApplicationService, ICompareTaskAppService
{
    private static readonly string[] AllowedExtensions = { ".pdf", ".doc", ".docx" };
    private const int MaxBidDocuments = 8;
    private const string ClauseExtractionSystemPrompt =
        "你是招投标文件分析助手。从用户提供的招标文件全文中提取所有强制性条款" +
        "（包含「须/应当/必须/不得/否则视为无效投标/废标」等强制措辞的条款）。" +
        "只返回 JSON 数组，不要输出任何其他文字。";

    internal static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IRepository<CompareTask, Guid> _taskRepository;
    private readonly IRepository<CompareDocument, Guid> _documentRepository;
    private readonly IRepository<EvidenceItem, Guid> _evidenceRepository;
    private readonly IRepository<ExportJob, Guid> _exportJobRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILlmGateway _llmGateway;
    private readonly ReportBuilder _reportBuilder;

    public CompareTaskAppService(
        IRepository<CompareTask, Guid> taskRepository,
        IRepository<CompareDocument, Guid> documentRepository,
        IRepository<EvidenceItem, Guid> evidenceRepository,
        IRepository<ExportJob, Guid> exportJobRepository,
        IFileStorage fileStorage,
        IBackgroundJobManager backgroundJobManager,
        ILlmGateway llmGateway,
        ReportBuilder reportBuilder)
    {
        _taskRepository = taskRepository;
        _documentRepository = documentRepository;
        _evidenceRepository = evidenceRepository;
        _exportJobRepository = exportJobRepository;
        _fileStorage = fileStorage;
        _backgroundJobManager = backgroundJobManager;
        _llmGateway = llmGateway;
        _reportBuilder = reportBuilder;
    }

    public async Task<CompareTaskDto> CreateAsync(CreateCompareTaskDto input)
    {
        var task = new CompareTask(GuidGenerator.Create(), input.Name.Trim());
        if (input.Clauses is { Count: > 0 })
        {
            var snapshot = BuildSnapshot(input.Clauses);
            task.LockClauseSnapshot(JsonSerializer.Serialize(snapshot, SnapshotJsonOptions));
        }

        await _taskRepository.InsertAsync(task, autoSave: true);
        return MapToDto(task, new List<CompareDocument>());
    }

    public async Task<CompareTaskDto> GetAsync(Guid id)
    {
        var task = await _taskRepository.GetAsync(id);
        var documents = await GetTaskDocumentsAsync(id);
        return MapToDto(task, documents);
    }

    public async Task<List<CompareDocumentDto>> GetDocumentsAsync(Guid id)
    {
        await _taskRepository.GetAsync(id);
        var documents = await GetTaskDocumentsAsync(id);
        return documents.OrderBy(d => d.CreationTime).Select(MapToDto).ToList();
    }

    public async Task<PagedResultDto<CompareTaskDto>> GetListAsync(GetCompareTasksInput input)
    {
        var queryable = await _taskRepository.GetQueryableAsync();
        queryable = queryable
            .WhereIf(!input.Name.IsNullOrWhiteSpace(), x => x.Name.Contains(input.Name!))
            .WhereIf(input.Status.HasValue, x => x.Status == input.Status!.Value);

        var totalCount = await AsyncExecuter.CountAsync(queryable);
        var tasks = await AsyncExecuter.ToListAsync(queryable
            .OrderByDescending(x => x.CreationTime)
            .PageBy(input.SkipCount, input.MaxResultCount));

        var taskIds = tasks.Select(x => x.Id).ToList();
        var docQueryable = await _documentRepository.GetQueryableAsync();
        var documents = await AsyncExecuter.ToListAsync(docQueryable.Where(d => taskIds.Contains(d.TaskId)));

        var items = tasks
            .Select(t => MapToDto(t, documents.Where(d => d.TaskId == t.Id).ToList()))
            .ToList();

        return new PagedResultDto<CompareTaskDto>(totalCount, items);
    }

    public async Task DeleteAsync(Guid id)
    {
        var task = await _taskRepository.GetAsync(id);
        var documents = await GetTaskDocumentsAsync(id);

        foreach (var document in documents)
        {
            await DeleteStorageQuietlyAsync(document.OriginStorageKey);
            if (document.IrStorageKey != null) await DeleteStorageQuietlyAsync(document.IrStorageKey);
            if (document.DocMdStorageKey != null) await DeleteStorageQuietlyAsync(document.DocMdStorageKey);
        }

        var evidenceQueryable = await _evidenceRepository.GetQueryableAsync();
        var evidences = await AsyncExecuter.ToListAsync(evidenceQueryable.Where(e => e.TaskId == id));
        await _evidenceRepository.DeleteManyAsync(evidences, autoSave: true);
        await _documentRepository.DeleteManyAsync(documents, autoSave: true);
        await _taskRepository.DeleteAsync(task, autoSave: true);
    }

    public async Task<CompareTaskDto> ReparseAsync(Guid id, ReparseDocumentsInput? input)
    {
        var task = await _taskRepository.GetAsync(id);
        var documents = await GetTaskDocumentsAsync(id);
        var failed = documents.Where(d => d.ParseStatus == DocumentParseStatus.Failed).ToList();
        if (failed.Count == 0)
        {
            throw new BusinessException(BidCompareErrorCodes.InvalidTaskState)
                .WithData("action", "Reparse")
                .WithData("status", "无失败文档");
        }

        List<CompareDocument> targets;
        if (input?.DocIds is { Count: > 0 })
        {
            var requested = input.DocIds.Distinct().ToList();
            var notFound = requested.Where(rid => documents.All(d => d.Id != rid)).ToList();
            if (notFound.Count > 0)
            {
                throw new BusinessException(BidCompareErrorCodes.DocumentNotFound)
                    .WithData("docIds", string.Join(",", notFound));
            }
            var notFailed = requested.Where(rid => failed.All(d => d.Id != rid)).ToList();
            if (notFailed.Count > 0)
            {
                throw new BusinessException(BidCompareErrorCodes.InvalidTaskState)
                    .WithData("action", "Reparse")
                    .WithData("docIds", string.Join(",", notFailed))
                    .WithData("reason", "仅支持重新解析失败文档");
            }
            targets = failed.Where(d => requested.Contains(d.Id)).ToList();
        }
        else
        {
            targets = failed;
        }

        task.RestartParsing();
        await _taskRepository.UpdateAsync(task, autoSave: true);

        foreach (var document in targets)
        {
            document.MarkPendingForReparse();
            await _documentRepository.UpdateAsync(document, autoSave: true);
            await _backgroundJobManager.EnqueueAsync(new ParseDocumentArgs { TaskId = id, DocumentId = document.Id });
        }

        return MapToDto(task, documents);
    }

    /// <summary>重新对比：重跑全部或指定比对对；任务正在 analyzing/comparing 时返回冲突。</summary>
    public async Task<CompareTaskDto> RetryCompareAsync(Guid id, RetryCompareInput? input)
    {
        var task = await _taskRepository.GetAsync(id);
        if (task.Status is CompareTaskStatus.Comparing or CompareTaskStatus.Analyzing)
        {
            throw new BusinessException(BidCompareErrorCodes.InvalidTaskState)
                .WithData("action", "RetryCompare")
                .WithData("status", task.Status.ToString())
                .WithData("reason", "任务正在分析中，请等待完成后重试");
        }

        var documents = await GetTaskDocumentsAsync(id);
        var parsedBidCount = documents.Count(d =>
            d.Role == DocumentRole.Bid && d.ParseStatus == DocumentParseStatus.Parsed);
        if (parsedBidCount < 2)
        {
            throw new BusinessException(BidCompareErrorCodes.InvalidTaskState)
                .WithData("action", "RetryCompare")
                .WithData("parsedBidCount", parsedBidCount)
                .WithData("reason", "可比对标书不足 2 份，请先重新解析失败文档");
        }

        if (input?.PairIds is { Count: > 0 })
        {
            var pairs = task.GetPairs();
            var unknown = input.PairIds.Where(pid => pairs.All(p => p.PairId != pid)).ToList();
            if (unknown.Count > 0)
            {
                throw new BusinessException(BidCompareErrorCodes.InvalidTaskState)
                    .WithData("action", "RetryCompare")
                    .WithData("pairIds", string.Join(",", unknown));
            }
        }

        task.MarkComparing();
        task.UpdateProgress("comparing", 60, "两两比对中");
        await _taskRepository.UpdateAsync(task, autoSave: true);
        await _backgroundJobManager.EnqueueAsync(new CompareDocumentsArgs
        {
            TaskId = id,
            PairIds = input?.PairIds
        });

        return MapToDto(task, documents);
    }

    /// <summary>编辑项目名：后端持久化 nameEditedByUser，前端据此决定是否自动应用建议名。</summary>
    public async Task<CompareTaskDto> UpdateNameAsync(Guid id, UpdateCompareTaskNameInput input)
    {
        var task = await _taskRepository.GetAsync(id);
        task.SetName(input.Name.Trim());
        await _taskRepository.UpdateAsync(task, autoSave: true);

        var documents = await GetTaskDocumentsAsync(id);
        return MapToDto(task, documents);
    }

    [DisableValidation] // Stream 参数无法被验证拦截器递归校验（ReadTimeout 等属性不可读）
    public async Task<CompareDocumentDto> UploadDocumentAsync(Guid id, DocumentRole role, string fileName, Stream content)
    {
        var task = await _taskRepository.GetAsync(id);

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            throw new BusinessException(BidCompareErrorCodes.UnsupportedFileType)
                .WithData("extension", extension);
        }

        var queryable = await _documentRepository.GetQueryableAsync();
        var bidCount = await AsyncExecuter.CountAsync(
            queryable.Where(d => d.TaskId == id && d.Role == DocumentRole.Bid));
        if (role == DocumentRole.Bid && bidCount >= MaxBidDocuments)
        {
            throw new BusinessException(BidCompareErrorCodes.DocumentCountOutOfRange)
                .WithData("min", 2)
                .WithData("max", MaxBidDocuments);
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer);
        var bytes = buffer.ToArray();

        var documentId = GuidGenerator.Create();
        var storageKey = $"compare/{id}/{documentId}/origin{extension}";
        await _fileStorage.UploadAsync(storageKey, new MemoryStream(bytes), ContentTypeOf(extension));

        var document = new CompareDocument(documentId, id, role, Path.GetFileName(fileName), bytes.Length, storageKey);
        await _documentRepository.InsertAsync(document, autoSave: true);

        if (role == DocumentRole.Tender)
        {
            task.SetTenderDocument(documentId);
            await _taskRepository.UpdateAsync(task, autoSave: true);
        }

        return MapToDto(document);
    }

    public async Task<CompareTaskDto> StartParsingAsync(Guid id)
    {
        var task = await _taskRepository.GetAsync(id);
        var documents = await GetTaskDocumentsAsync(id);
        var pendingIds = documents
            .Where(d => d.ParseStatus == DocumentParseStatus.Pending)
            .Select(d => d.Id)
            .ToList();

        if (pendingIds.Count > 0)
        {
            await _backgroundJobManager.EnqueueAsync(new ParseDocumentsArgs
            {
                TaskId = id,
                DocumentIds = pendingIds
            });
        }

        return MapToDto(task, documents);
    }

    public async Task<CompareDocumentFileResult> GetDocumentFileAsync(Guid id, Guid docId)
    {
        await _taskRepository.GetAsync(id); // 任务不存在 → 404
        var document = await _documentRepository.FirstOrDefaultAsync(d => d.TaskId == id && d.Id == docId);
        if (document == null)
        {
            throw new BusinessException(BidCompareErrorCodes.DocumentNotFound).WithData("docId", docId);
        }

        var content = await _fileStorage.GetAsync(document.OriginStorageKey);
        var extension = Path.GetExtension(document.FileName).ToLowerInvariant();
        return new CompareDocumentFileResult
        {
            Content = content,
            ContentType = ContentTypeOf(extension),
            FileName = document.FileName,
        };
    }

    public async Task<DocumentIrDto> GetDocumentIrAsync(Guid id, Guid docId)
    {
        await _taskRepository.GetAsync(id); // 任务不存在 → 404
        var document = await _documentRepository.FirstOrDefaultAsync(d => d.TaskId == id && d.Id == docId);
        if (document == null)
        {
            throw new BusinessException(BidCompareErrorCodes.DocumentNotFound).WithData("docId", docId);
        }
        if (document.ParseStatus != DocumentParseStatus.Parsed || document.IrStorageKey == null)
        {
            throw new BusinessException(BidCompareErrorCodes.IrNotReady).WithData("docId", docId);
        }

        await using var stream = await _fileStorage.GetAsync(document.IrStorageKey);
        var ir = await JsonSerializer.DeserializeAsync<DocumentIrDto>(stream, SnapshotJsonOptions);
        return ir!;
    }

    public async Task<PagedResultDto<EvidenceDto>> GetEvidencesAsync(Guid id, GetEvidenceListInput input)
    {
        await _taskRepository.GetAsync(id);

        var queryable = await _evidenceRepository.GetQueryableAsync();
        queryable = queryable
            .Where(e => e.TaskId == id)
            .WhereIf(input.Type.HasValue, e => e.Type == input.Type!.Value)
            .WhereIf(input.Severity.HasValue, e => e.Severity == input.Severity!.Value);

        // 文档对过滤涉及 JSON 负载，原型规模（单任务证据量有限）在内存过滤后再分页
        var entities = await AsyncExecuter.ToListAsync(queryable.OrderBy(e => e.Severity).ThenBy(e => e.CreationTime));
        var dtos = entities.Select(EvidenceMapper.ToDto).ToList();

        if (input.DocIdA.HasValue && input.DocIdB.HasValue)
        {
            dtos = dtos.Where(e => e.DocIds.Contains(input.DocIdA.Value) && e.DocIds.Contains(input.DocIdB.Value)).ToList();
        }

        return new PagedResultDto<EvidenceDto>(
            dtos.Count,
            dtos.Skip(input.SkipCount).Take(input.MaxResultCount).ToList());
    }

    public async Task<SimilarityMatrixDto> GetMatrixAsync(Guid id)
    {
        await _taskRepository.GetAsync(id);

        var docQueryable = await _documentRepository.GetQueryableAsync();
        var docs = await AsyncExecuter.ToListAsync(docQueryable
            .Where(d => d.TaskId == id && d.Role == DocumentRole.Bid && d.ParseStatus == DocumentParseStatus.Parsed)
            .OrderBy(d => d.CreationTime));

        var evQueryable = await _evidenceRepository.GetQueryableAsync();
        var similarityEvidences = await AsyncExecuter.ToListAsync(
            evQueryable.Where(e => e.TaskId == id && e.Type == EvidenceType.Similarity));

        var pairs = similarityEvidences
            .Select(e => (DocIds: EvidenceMapper.DeserializeDocIds(e.DocIdsJson),
                          Similarity: EvidenceMapper.ReadSimilarity(e.MetricsJson)))
            .ToList();

        var cells = new List<SimilarityMatrixCellDto>();
        foreach (var a in docs)
        {
            foreach (var b in docs)
            {
                var similarity = a.Id == b.Id
                    ? 1.0
                    : pairs.Where(p => p.Similarity.HasValue && p.DocIds.Contains(a.Id) && p.DocIds.Contains(b.Id))
                           .Select(p => p.Similarity!.Value)
                           .DefaultIfEmpty(0.0)
                           .Max();
                cells.Add(new SimilarityMatrixCellDto
                {
                    DocAId = a.Id,
                    DocBId = b.Id,
                    Similarity = Math.Round(similarity, 4)
                });
            }
        }

        return new SimilarityMatrixDto
        {
            DocIds = docs.Select(d => d.Id).ToList(),
            Cells = cells
        };
    }

    public async Task<List<ClauseDto>> ExtractClausesAsync(Guid id)
    {
        var task = await _taskRepository.GetAsync(id);
        if (!task.TenderDocumentId.HasValue)
        {
            throw new BusinessException(BidCompareErrorCodes.NoTenderDocument).WithData("taskId", id);
        }

        var tenderDoc = await _documentRepository.GetAsync(task.TenderDocumentId.Value);
        if (tenderDoc.ParseStatus != DocumentParseStatus.Parsed || tenderDoc.DocMdStorageKey == null)
        {
            throw new BusinessException(BidCompareErrorCodes.IrNotReady).WithData("docId", tenderDoc.Id);
        }

        string docMd;
        await using (var stream = await _fileStorage.GetAsync(tenderDoc.DocMdStorageKey))
        using (var reader = new StreamReader(stream))
        {
            docMd = await reader.ReadToEndAsync();
        }

        var userPrompt =
            "以下是招标文件全文（Markdown）：\n\n" + docMd +
            "\n\n请以 JSON 数组返回全部强制性条款，每项字段：text（条款原文）、mandatory（是否强制，bool）、category（分类，如 资质/报价/技术/工期/格式）。只返回 JSON。";

        var response = await _llmGateway.CompleteAsync(ClauseExtractionSystemPrompt, userPrompt);

        return ParseClauseDrafts(response);
    }

    public async Task<CompareTaskDto> ConfirmClausesAsync(Guid id, ConfirmClausesInput input)
    {
        var task = await _taskRepository.GetAsync(id);
        var snapshot = BuildSnapshot(input.Clauses);
        task.LockClauseSnapshot(JsonSerializer.Serialize(snapshot, SnapshotJsonOptions));
        task.MarkComparing();
        task.UpdateProgress("comparing", 60, "两两比对中");
        await _taskRepository.UpdateAsync(task, autoSave: true);

        await _backgroundJobManager.EnqueueAsync(new CompareDocumentsArgs { TaskId = id });

        var documents = await GetTaskDocumentsAsync(id);
        return MapToDto(task, documents);
    }

    public async Task<CompareReportDto> GetReportAsync(Guid id)
    {
        var task = await _taskRepository.GetAsync(id);

        if (task.ReportJson != null)
        {
            return JsonSerializer.Deserialize<CompareReportDto>(task.ReportJson, SnapshotJsonOptions)!;
        }
        if (task.Status != CompareTaskStatus.Done)
        {
            throw new BusinessException(BidCompareErrorCodes.ReportNotReady).WithData("taskId", id);
        }

        var report = await _reportBuilder.BuildAsync(id);
        task.SetReport(JsonSerializer.Serialize(report, SnapshotJsonOptions), Clock.Now);
        await _taskRepository.UpdateAsync(task, autoSave: true);
        return report;
    }

    public async Task<ExportJobDto> RequestExportAsync(Guid id, ExportRequestDto input)
    {
        var task = await _taskRepository.GetAsync(id);
        if (task.Status != CompareTaskStatus.Done)
        {
            throw new BusinessException(BidCompareErrorCodes.ReportNotReady).WithData("taskId", id);
        }

        var job = new ExportJob(GuidGenerator.Create(), id, input.Format);
        await _exportJobRepository.InsertAsync(job, autoSave: true);
        await _backgroundJobManager.EnqueueAsync(new ExportReportArgs { ExportJobId = job.Id });
        return MapToDto(job, downloadUrl: null);
    }

    public async Task<ExportJobDto> GetExportJobAsync(Guid id, Guid jobId)
    {
        await _taskRepository.GetAsync(id);
        var job = await _exportJobRepository.GetAsync(jobId);
        if (job.TaskId != id)
        {
            throw new BusinessException(BidCompareErrorCodes.ExportJobNotFound).WithData("jobId", jobId);
        }

        var downloadUrl = job.Status == ExportJobStatus.Succeeded && job.FileStorageKey != null
            ? await _fileStorage.GetPresignedUrlAsync(job.FileStorageKey, TimeSpan.FromHours(1))
            : null;
        return MapToDto(job, downloadUrl);
    }

    private static ExportJobDto MapToDto(ExportJob job, string? downloadUrl) => new()
    {
        JobId = job.Id,
        TaskId = job.TaskId,
        Format = job.Format,
        Status = job.Status,
        DownloadUrl = downloadUrl,
        Error = job.Error
    };

    /// <summary>解析 LLM 条款提取响应：剥离 ```json 围栏后按数组解析，异常即抛 IrValidationFailed。</summary>
    internal static List<ClauseDto> ParseClauseDrafts(string llmResponse)
    {
        var json = llmResponse.Trim();
        if (json.StartsWith("```"))
        {
            var firstNewline = json.IndexOf('\n');
            var lastFence = json.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline > 0 && lastFence > firstNewline)
            {
                json = json[(firstNewline + 1)..lastFence].Trim();
            }
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var drafts = new List<ClauseDto>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var text = element.TryGetProperty("text", out var t) ? t.GetString() : null;
                if (text.IsNullOrWhiteSpace())
                {
                    continue;
                }
                drafts.Add(new ClauseDto
                {
                    ClauseId = Guid.NewGuid().ToString("N"),
                    Source = ClauseSource.Extracted,
                    Text = text!,
                    Mandatory = element.TryGetProperty("mandatory", out var m) && m.ValueKind == JsonValueKind.True,
                    Category = element.TryGetProperty("category", out var c) && c.ValueKind == JsonValueKind.String
                        ? c.GetString()
                        : null
                });
            }
            return drafts;
        }
        catch (JsonException ex)
        {
            throw new BusinessException(BidCompareErrorCodes.IrValidationFailed)
                .WithData("reason", $"LLM 条款提取响应不是合法 JSON：{ex.Message}");
        }
    }

    internal static List<ClauseSnapshotItem> BuildSnapshot(IEnumerable<ClauseInputDto> clauses)
    {
        return clauses.Select(c => new ClauseSnapshotItem
        {
            ClauseId = c.ClauseId.IsNullOrWhiteSpace() ? Guid.NewGuid().ToString("N") : c.ClauseId!,
            Source = c.Source ?? ClauseSource.Manual,
            Text = c.Text.Trim(),
            Mandatory = c.Mandatory,
            Category = c.Category
        }).ToList();
    }

    internal static CompareTaskDto MapToDto(CompareTask task, List<CompareDocument> documents)
    {
        var pairs = task.PairsJson == null ? null : task.GetPairs();
        int? pairIndex = null;
        if (pairs is { Count: > 0 })
        {
            var doneCount = pairs.Count(p => p.Status is ComparePairStatus.Done or ComparePairStatus.Failed);
            var processing = pairs.FindIndex(p => p.Status == ComparePairStatus.Processing);
            var waiting = pairs.FindIndex(p => p.Status == ComparePairStatus.Waiting);
            if (processing >= 0)
            {
                pairIndex = processing + 1;
            }
            else if (doneCount == pairs.Count)
            {
                pairIndex = pairs.Count;
            }
            else if (waiting >= 0 && doneCount > 0)
            {
                // 已有完成对、还有等待对 → 展示下一个即将落定的对
                pairIndex = waiting + 1;
            }
            // 全部 waiting（批处理计算中）：pairIndex 保持 null，前端不展示「第 i/N 对」
        }

        return new CompareTaskDto
        {
            Id = task.Id,
            Name = task.Name,
            NameEditedByUser = task.NameEditedByUser,
            SuggestedName = task.SuggestedName,
            Status = task.Status,
            FailureReason = task.FailureReason,
            DocIds = documents.OrderBy(d => d.CreationTime).Select(d => d.Id).ToList(),
            TenderDocId = task.TenderDocumentId,
            ClauseSnapshot = task.ClauseSnapshotJson == null
                ? null
                : JsonSerializer.Deserialize<List<ClauseDto>>(task.ClauseSnapshotJson, SnapshotJsonOptions),
            Progress = new CompareProgressDto
            {
                Stage = task.ProgressStage,
                Percent = task.ProgressPercent,
                Message = task.ProgressMessage,
                PairIndex = pairIndex,
                PairCount = pairs?.Count
            },
            Pairs = pairs?.Select(MapToPairDto).ToList(),
            CreatedAt = task.CreationTime
        };
    }

    internal static ComparePairDto MapToPairDto(ComparePairItem pair)
    {
        return new ComparePairDto
        {
            PairId = pair.PairId,
            DocAId = pair.DocAId,
            DocBId = pair.DocBId,
            Status = pair.Status,
            Similarity = pair.Similarity,
            FailReason = pair.FailReason,
            StartedAt = pair.StartedAt,
            FinishedAt = pair.FinishedAt
        };
    }

    internal static CompareDocumentDto MapToDto(CompareDocument document)
    {
        return new CompareDocumentDto
        {
            Id = document.Id,
            TaskId = document.TaskId,
            Role = document.Role,
            FileName = document.FileName,
            FileSize = document.FileSize,
            ParseStatus = document.ParseStatus,
            ParseError = document.ParseError,
            PageCount = document.PageCount,
            OcrLowConfidenceRatio = document.OcrLowConfidenceRatio,
            CreatedAt = document.CreationTime
        };
    }

    private async Task<List<CompareDocument>> GetTaskDocumentsAsync(Guid taskId)
    {
        var queryable = await _documentRepository.GetQueryableAsync();
        return await AsyncExecuter.ToListAsync(queryable.Where(d => d.TaskId == taskId));
    }

    private async Task DeleteStorageQuietlyAsync(string key)
    {
        try
        {
            await _fileStorage.DeleteAsync(key);
        }
        catch
        {
            // 对象存储删除失败不阻塞任务删除（孤儿对象由运维清理）
        }
    }

    private static string ContentTypeOf(string extension) => extension switch
    {
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        _ => "application/octet-stream"
    };
}
