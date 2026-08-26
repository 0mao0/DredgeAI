using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Analysis;
using DredgeAI.BidCompare.BackgroundJobs;
using DredgeAI.BidCompare.Clauses;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Drafts;
using DredgeAI.BidCompare.Evidences;
using DredgeAI.BidCompare.Exports;
using DredgeAI.BidCompare.Ir;
using DredgeAI.BidCompare.Reports;
using DredgeAI.BidCompare.Reporting;
using DredgeAI.BidCompare.Storage;
using DredgeAI.BidCompare.TenderReadings;
using Microsoft.Extensions.Logging;
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
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> PreviewConvertLocks = new();

    /// <summary>上传闸门（按任务粒度串行化「计数检查 + 落库」，用后移除防字典膨胀）。</summary>
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> UploadGates = new();

    /// <summary>IR 读缓存：key 含 ParseFinishedAt（重解析后自然失效），TTL 5 分钟，上限 64 份。</summary>
    private static readonly ConcurrentDictionary<string, (DateTime ExpiresAt, DocumentIrDto Ir)> IrCache = new();
    private static readonly TimeSpan IrCacheTtl = TimeSpan.FromMinutes(5);
    private const int IrCacheMaxEntries = 64;
    internal static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IRepository<CompareTask, Guid> _taskRepository;
    private readonly IRepository<CompareDocument, Guid> _documentRepository;
    private readonly IRepository<CompareDraftDocument, Guid> _draftDocumentRepository;
    private readonly IRepository<EvidenceItem, Guid> _evidenceRepository;
    private readonly IRepository<ExportJob, Guid> _exportJobRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IPdfConverter _pdfConverter;
    private readonly ReportBuilder _reportBuilder;
    private readonly IRepository<TenderReadingTask, Guid> _tenderReadingTaskRepository;
    private readonly BaselineStore _baselineStore;

    public CompareTaskAppService(
        IRepository<CompareTask, Guid> taskRepository,
        IRepository<CompareDocument, Guid> documentRepository,
        IRepository<CompareDraftDocument, Guid> draftDocumentRepository,
        IRepository<EvidenceItem, Guid> evidenceRepository,
        IRepository<ExportJob, Guid> exportJobRepository,
        IFileStorage fileStorage,
        IBackgroundJobManager backgroundJobManager,
        IPdfConverter pdfConverter,
        IRepository<TenderReadingTask, Guid> tenderReadingTaskRepository,
        BaselineStore baselineStore,
        ReportBuilder reportBuilder)
    {
        _taskRepository = taskRepository;
        _documentRepository = documentRepository;
        _draftDocumentRepository = draftDocumentRepository;
        _evidenceRepository = evidenceRepository;
        _exportJobRepository = exportJobRepository;
        _fileStorage = fileStorage;
        _backgroundJobManager = backgroundJobManager;
        _pdfConverter = pdfConverter;
        _reportBuilder = reportBuilder;
        _tenderReadingTaskRepository = tenderReadingTaskRepository;
        _baselineStore = baselineStore;
    }

    public async Task<CompareTaskDto> CreateAsync(CreateCompareTaskDto input)
    {
        var task = new CompareTask(GuidGenerator.Create(), input.Name.Trim());
        await _taskRepository.InsertAsync(task, autoSave: true);


        if (input.TenderReadingTaskId.HasValue)
        {
            var tenderTask = await _tenderReadingTaskRepository.GetAsync(input.TenderReadingTaskId.Value);
            if (tenderTask.Status != TenderReadingTaskStatus.Ready)
            {
                throw new BusinessException(BidCompareErrorCodes.InvalidTaskState)
                    .WithData("action", "CreateFromTenderReading")
                    .WithData("status", tenderTask.Status.ToString())
                    .WithData("reason", "读标基准库尚未 Ready，不能用于创建比标任务");
            }

            var baseline = await _baselineStore.GetBaselineAsync(tenderTask.Id);
            var clauseInputs = baseline.Fields
                .Where(f => f.Category == BaselineCategory.RejectionClauses)
                .Select(f => BuildClauseInputFromBaselineField(f))
                .Where(c => !string.IsNullOrWhiteSpace(c.Text))
                .ToList();

            if (clauseInputs.Any())
            {
                var snapshot = BuildSnapshot(clauseInputs);
                task.LockClauseSnapshot(JsonSerializer.Serialize(snapshot, SnapshotJsonOptions));
            }

            task.AttachTenderReadingBaseline(tenderTask.Id);
            await _taskRepository.UpdateAsync(task, autoSave: true);
        }

        if (input.Clauses is { Count: > 0 })
        {
            var snapshot = BuildSnapshot(input.Clauses);
            task.LockClauseSnapshot(JsonSerializer.Serialize(snapshot, SnapshotJsonOptions));
            await _taskRepository.UpdateAsync(task, autoSave: true);
        }

        var documents = new List<CompareDocument>();
        if (input.DraftId.HasValue)
        {
            var draftQueryable = await _draftDocumentRepository.GetQueryableAsync();
            var draftDocuments = await AsyncExecuter.ToListAsync(draftQueryable
                .Where(d => d.DraftId == input.DraftId.Value)
                .OrderBy(d => d.CreationTime));

            var draftBidCount = draftDocuments.Count(d => d.Role == DocumentRole.Bid);
            if (draftBidCount < 2)
            {
                throw new BusinessException(BidCompareErrorCodes.InvalidTaskState)
                    .WithData("action", "CreateFromDraft")
                    .WithData("bidCount", draftBidCount)
                    .WithData("reason", "投标文件不足 2 份，无法开始解析");
            }

            foreach (var draft in draftDocuments)
            {
                var document = new CompareDocument(
                    GuidGenerator.Create(),
                    task.Id,
                    draft.Role,
                    draft.FileName,
                    draft.FileSize,
                    draft.OriginStorageKey);
                documents.Add(document);
                await _documentRepository.InsertAsync(document, autoSave: true);

                if (draft.Role == DocumentRole.Tender)
                {
                    task.SetTenderDocument(document.Id);
                    await _taskRepository.UpdateAsync(task, autoSave: true);
                }
            }

            await _draftDocumentRepository.DeleteManyAsync(draftDocuments, autoSave: true);
        }

        return MapToDto(task, documents);
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

        // 存储按任务前缀整树清理（含 raw/ 原始产物、preview.pdf、exports/ 等孤儿对象）
        await DeleteStoragePrefixQuietlyAsync($"compare/{id}/");

        var evidenceQueryable = await _evidenceRepository.GetQueryableAsync();
        var evidences = await AsyncExecuter.ToListAsync(evidenceQueryable.Where(e => e.TaskId == id));
        await _evidenceRepository.DeleteManyAsync(evidences, autoSave: true);

        var exportQueryable = await _exportJobRepository.GetQueryableAsync();
        var exportJobs = await AsyncExecuter.ToListAsync(exportQueryable.Where(j => j.TaskId == id));
        await _exportJobRepository.DeleteManyAsync(exportJobs, autoSave: true);

        await _documentRepository.DeleteManyAsync(documents, autoSave: true);
        await _taskRepository.DeleteAsync(task, autoSave: true);
    }

    public async Task<CompareTaskDto> ReparseAsync(Guid id, ReparseDocumentsInput? input)
    {
        var task = await _taskRepository.GetAsync(id);
        var documents = await GetTaskDocumentsAsync(id);
        // 与运行中的解析互斥：进行中重复触发会导致同一文档被重复提交 AnGIneer
        if (documents.Any(d => d.ParseStatus == DocumentParseStatus.Parsing))
        {
            throw new BusinessException(BidCompareErrorCodes.InvalidTaskState)
                .WithData("action", "Reparse")
                .WithData("reason", "存在解析中的文档，请等待当前解析结束后再重试");
        }
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
        try
        {
            await _taskRepository.UpdateAsync(task, autoSave: true);
        }
        catch (Exception ex) when (DbConcurrency.IsConflict(ex))
        {
            throw DbConcurrency.ToInvalidState("Reparse");
        }

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
        try
        {
            await _taskRepository.UpdateAsync(task, autoSave: true);
        }
        catch (Exception ex) when (DbConcurrency.IsConflict(ex))
        {
            throw DbConcurrency.ToInvalidState("RetryCompare");
        }
        await _backgroundJobManager.EnqueueAsync(new CompareDocumentsArgs
        {
            TaskId = id,
            PairIds = input?.PairIds
        });

        return MapToDto(task, documents);
    }

    /// <summary>重新抽取 AI 分析（关键指标 + 条款响应矩阵），不重跑两两对比。</summary>
    public async Task<CompareTaskDto> RetryAiAnalysisAsync(Guid id)
    {
        var task = await _taskRepository.GetAsync(id);
        if (task.Status is CompareTaskStatus.Comparing or CompareTaskStatus.Analyzing)
        {
            throw new BusinessException(BidCompareErrorCodes.InvalidTaskState)
                .WithData("action", "RetryAiAnalysis")
                .WithData("status", task.Status.ToString())
                .WithData("reason", "任务正在分析中，请等待完成后再试");
        }
        if (task.Status is not (CompareTaskStatus.Done or CompareTaskStatus.Partial))
        {
            throw new BusinessException(BidCompareErrorCodes.InvalidTaskState)
                .WithData("action", "RetryAiAnalysis")
                .WithData("status", task.Status.ToString())
                .WithData("reason", "任务尚未进入 AI 分析阶段");
        }

        task.MarkAnalyzing();
        task.UpdateProgress("analyzing", 80, "AI 分析中");
        try
        {
            await _taskRepository.UpdateAsync(task, autoSave: true);
        }
        catch (Exception ex) when (DbConcurrency.IsConflict(ex))
        {
            throw DbConcurrency.ToInvalidState("RetryAiAnalysis");
        }
        await _backgroundJobManager.EnqueueAsync(new AiAnalysisArgs { TaskId = id });

        var documents = await GetTaskDocumentsAsync(id);
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

        // 上传闸门：计数检查与落库串行化，并发上传不会突破 8 份上限
        var gate = UploadGates.GetOrAdd(id, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            var bidCount = await AsyncExecuter.CountAsync(
                queryable.Where(d => d.TaskId == id && d.Role == DocumentRole.Bid));
            if (role == DocumentRole.Bid && bidCount >= MaxBidDocuments)
            {
                throw new BusinessException(BidCompareErrorCodes.DocumentCountOutOfRange)
                    .WithData("min", 2)
                    .WithData("max", MaxBidDocuments);
            }

        // 魔数嗅探仅作提示：AnGIneer 侧 .doc/.docx 统一走 LibreOffice 按内容识别转换，
        // 扩展名与内容不一致不影响解析，因此不再拦截，只记录警告（前端会上传前本地提示）。
        var header = new byte[8];
        var headerLength = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        if (!UploadFileSignature.Matches(extension, header.AsSpan(0, headerLength)))
        {
            Logger.LogWarning(
                "上传文件 {FileName}（扩展名 {Extension}）内容与扩展名不符（魔数校验失败），已按内容继续处理",
                fileName,
                extension);
        }

        // 流式直通存储（不整文件内存缓冲），实际上传字节数由直通流统计
        var documentId = GuidGenerator.Create();
        var storageKey = $"compare/{id}/{documentId}/origin{extension}";
        var uploadStream = new PrefixCountingStream(header, headerLength, content);
        await _fileStorage.UploadAsync(storageKey, uploadStream, ContentTypeOf(extension));

        var document = new CompareDocument(documentId, id, role, Path.GetFileName(fileName), uploadStream.TotalBytesRead, storageKey);
        await _documentRepository.InsertAsync(document, autoSave: true);

            if (role == DocumentRole.Tender)
            {
                task.SetTenderDocument(documentId);
                await _taskRepository.UpdateAsync(task, autoSave: true);
            }

            return MapToDto(document);
        }
        finally
        {
            gate.Release();
            if (gate.CurrentCount == 1)
            {
                UploadGates.TryRemove(id, out _);
            }
        }
    }

    public async Task<CompareTaskDto> StartParsingAsync(Guid id)
    {
        var task = await _taskRepository.GetAsync(id);
        if (task.Status is CompareTaskStatus.Comparing or CompareTaskStatus.Analyzing or CompareTaskStatus.Done)
        {
            throw new BusinessException(BidCompareErrorCodes.InvalidTaskState)
                .WithData("action", "StartParsing")
                .WithData("status", task.Status.ToString())
                .WithData("reason", "任务已进入比对阶段，不能重复触发解析");
        }

        var documents = await GetTaskDocumentsAsync(id);
        // 幂等守卫：解析进行中重复触发会导致同一批文档被重复提交 AnGIneer
        if (documents.Any(d => d.ParseStatus == DocumentParseStatus.Parsing))
        {
            throw new BusinessException(BidCompareErrorCodes.InvalidTaskState)
                .WithData("action", "StartParsing")
                .WithData("reason", "解析进行中，请勿重复触发");
        }
        var pendingIds = documents
            .Where(d => d.ParseStatus == DocumentParseStatus.Pending)
            .Select(d => d.Id)
            .ToList();

        if (pendingIds.Count > 0)
        {
            // 并发防护：先预标记解析中并更新任务行（ConcurrencyStamp 相当于比较并交换），
            // 并发重复触发会在任务更新处冲突抛 409，不会双份入队重复提交 AnGIneer
            foreach (var document in documents.Where(d => pendingIds.Contains(d.Id)))
            {
                document.MarkParsing();
                await _documentRepository.UpdateAsync(document, autoSave: true);
            }
            try
            {
                task.UpdateProgress("parsing", 0, "解析队列已提交");
                await _taskRepository.UpdateAsync(task, autoSave: true);
            }
            catch (Exception ex) when (DbConcurrency.IsConflict(ex))
            {
                throw DbConcurrency.ToInvalidState("StartParsing");
            }
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

        var extension = Path.GetExtension(document.FileName).ToLowerInvariant();
        // Word 文档在线预览：首次请求用 LibreOffice 转 PDF 并缓存（约定 key preview.pdf），
        // 之后直接返回缓存 PDF；转换失败回退返回原始文件（前端按文件类型降级为「暂不支持在线预览」）。
        if (extension is ".doc" or ".docx")
        {
            var previewKey = $"compare/{document.TaskId}/{document.Id}/preview.pdf";
            var convertLock = PreviewConvertLocks.GetOrAdd(previewKey, _ => new SemaphoreSlim(1, 1));
            await convertLock.WaitAsync();
            try
            {
                if (await _fileStorage.ExistsAsync(previewKey))
                {
                    return new CompareDocumentFileResult
                    {
                        Content = await _fileStorage.GetAsync(previewKey),
                        ContentType = "application/pdf",
                        FileName = Path.GetFileNameWithoutExtension(document.FileName) + ".pdf",
                    };
                }

                try
                {
                    await using var origin = await _fileStorage.GetAsync(document.OriginStorageKey);
                    using var originBuffer = new MemoryStream();
                    await origin.CopyToAsync(originBuffer);
                    var pdfBytes = await _pdfConverter.ConvertToPdfAsync(originBuffer.ToArray());
                    await using var pdfStream = new MemoryStream(pdfBytes);
                    await _fileStorage.UploadAsync(previewKey, pdfStream, "application/pdf");
                    return new CompareDocumentFileResult
                    {
                        Content = new MemoryStream(pdfBytes),
                        ContentType = "application/pdf",
                        FileName = Path.GetFileNameWithoutExtension(document.FileName) + ".pdf",
                    };
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Word 文档转 PDF 预览失败，回退返回原始文件：{Key}", document.OriginStorageKey);
                    return new CompareDocumentFileResult
                    {
                        Content = await _fileStorage.GetAsync(document.OriginStorageKey),
                        ContentType = ContentTypeOf(extension),
                        FileName = document.FileName,
                    };
                }
            }
            finally
            {
                convertLock.Release();
                // 锁用后即移除，避免字典随文档数无限增长（仍有等待者时 CurrentCount==0，跳过移除）
                if (convertLock.CurrentCount == 1)
                {
                    PreviewConvertLocks.TryRemove(previewKey, out _);
                }
            }
        }

        var content = await _fileStorage.GetAsync(document.OriginStorageKey);
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

        // IR 读缓存：key 含 ParseFinishedAt，重解析后自然失效；TTL 5 分钟，上限惰性清理
        var cacheKey = $"{document.Id}:{document.ParseFinishedAt?.Ticks ?? 0}";
        if (IrCache.TryGetValue(cacheKey, out var hit) && hit.ExpiresAt > DateTime.UtcNow)
        {
            return hit.Ir;
        }

        await using var stream = await _fileStorage.GetAsync(document.IrStorageKey);
        var ir = await JsonSerializer.DeserializeAsync<DocumentIrDto>(stream, SnapshotJsonOptions);
        if (ir == null)
        {
            return ir!;
        }
        if (IrCache.Count >= IrCacheMaxEntries)
        {
            var now = DateTime.UtcNow;
            foreach (var kv in IrCache.Where(kv => kv.Value.ExpiresAt <= now))
            {
                IrCache.TryRemove(kv.Key, out _);
            }
            if (IrCache.Count >= IrCacheMaxEntries)
            {
                IrCache.Clear();
            }
        }
        IrCache[cacheKey] = (DateTime.UtcNow.Add(IrCacheTtl), ir);
        return ir;
    }

    public async Task<PagedResultDto<EvidenceDto>> GetEvidencesAsync(Guid id, GetEvidenceListInput input)
    {
        await _taskRepository.GetAsync(id);

        var queryable = await _evidenceRepository.GetQueryableAsync();
        queryable = queryable
            .Where(e => e.TaskId == id)
            .WhereIf(input.Type.HasValue, e => e.Type == input.Type!.Value)
            .WhereIf(input.Severity.HasValue, e => e.Severity == input.Severity!.Value);

        // Type/Severity 过滤下沉 DB；matrixOnly 与文档对（DocIdA/B）过滤依赖 JSON 字段无法下推 SQL，
        // 原型规模在内存完成过滤与分页/计数
        var all = await AsyncExecuter.ToListAsync(
            queryable.OrderBy(e => e.Severity).ThenBy(e => e.CreationTime));
        var dtos = all
            .Where(e => !IsMatrixOnlyEvidence(e))
            .Select(EvidenceMapper.ToDto)
            .Where(e => !input.DocIdA.HasValue || !input.DocIdB.HasValue
                || (e.DocIds.Contains(input.DocIdA.Value) && e.DocIds.Contains(input.DocIdB.Value)))
            .ToList();
        return new PagedResultDto<EvidenceDto>(
            dtos.Count,
            dtos.Skip(input.SkipCount).Take(input.MaxResultCount).ToList());
    }

    private static bool IsMatrixOnlyEvidence(EvidenceItem e)
        => e.Type == EvidenceType.Similarity && EvidenceMapper.ReadMatrixOnly(e.MetricsJson);

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

    /// <summary>触发条款提取（异步）：校验状态后入队后台作业，草案就绪/失败由任务轮询感知。</summary>
    public async Task<CompareTaskDto> ExtractClausesAsync(Guid id)
    {
        var task = await _taskRepository.GetAsync(id);
        if (task.Status is CompareTaskStatus.Comparing or CompareTaskStatus.Analyzing or CompareTaskStatus.Done)
        {
            throw new BusinessException(BidCompareErrorCodes.InvalidTaskState)
                .WithData("action", "ExtractClauses")
                .WithData("status", task.Status.ToString())
                .WithData("reason", "当前任务状态不允许提取条款");
        }
        if (!task.TenderDocumentId.HasValue)
        {
            throw new BusinessException(BidCompareErrorCodes.NoTenderDocument).WithData("taskId", id);
        }

        task.UpdateProgress("clauses_extracting", 40, "条款提取中");
        try
        {
            await _taskRepository.UpdateAsync(task, autoSave: true);
        }
        catch (Exception ex) when (DbConcurrency.IsConflict(ex))
        {
            throw DbConcurrency.ToInvalidState("ExtractClauses");
        }

        await _backgroundJobManager.EnqueueAsync(new ExtractClausesArgs { TaskId = id });

        var documents = await GetTaskDocumentsAsync(id);
        return MapToDto(task, documents);
    }

    public async Task<CompareTaskDto> ConfirmClausesAsync(Guid id, ConfirmClausesInput input)
    {
        var task = await _taskRepository.GetAsync(id);
        var snapshot = BuildSnapshot(input.Clauses);
        task.LockClauseSnapshot(JsonSerializer.Serialize(snapshot, SnapshotJsonOptions));
        task.ClearClauseDrafts();
        task.MarkComparing();
        task.UpdateProgress("comparing", 60, "两两比对中");
        try
        {
            await _taskRepository.UpdateAsync(task, autoSave: true);
        }
        catch (Exception ex) when (DbConcurrency.IsConflict(ex))
        {
            throw DbConcurrency.ToInvalidState("ConfirmClauses");
        }

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


    private static ClauseInputDto BuildClauseInputFromBaselineField(BaselineFieldDto field)
    {
        var text = field.RawText;
        var mandatory = false;
        var category = field.Category.ToString();

        try
        {
            using var doc = JsonDocument.Parse(field.ValueJson);
            if (doc.RootElement.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
            {
                text = textProp.GetString();
            }

            if (doc.RootElement.TryGetProperty("mandatory", out var mandatoryProp)
                && mandatoryProp.ValueKind == JsonValueKind.True)
            {
                mandatory = true;
            }

            if (doc.RootElement.TryGetProperty("category", out var categoryProp)
                && categoryProp.ValueKind == JsonValueKind.String)
            {
                category = categoryProp.GetString();
            }
        }
        catch (JsonException)
        {
            // 保留 RawText
        }

        return new ClauseInputDto
        {
            Text = string.IsNullOrWhiteSpace(text) ? field.RawText : text!,
            Mandatory = mandatory,
            Category = category
        };
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
            TenderReadingTaskId = task.TenderReadingTaskId,
            ClauseSnapshot = task.ClauseSnapshotJson == null
                ? null
                : JsonSerializer.Deserialize<List<ClauseDto>>(task.ClauseSnapshotJson, SnapshotJsonOptions),
            ClauseDrafts = task.ClauseDraftsJson == null
                ? null
                : JsonSerializer.Deserialize<List<ClauseDto>>(task.ClauseDraftsJson, SnapshotJsonOptions),
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
            ParseProgress = document.ParseProgress,
            ParseStage = document.ParseStage,
            ParseStageMessage = document.ParseStageMessage,
            ParseStartedAt = document.ParseStartedAt,
            ParseFinishedAt = document.ParseFinishedAt,
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

    private async Task DeleteStoragePrefixQuietlyAsync(string prefix)
    {
        try
        {
            await _fileStorage.DeleteByPrefixAsync(prefix);
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
