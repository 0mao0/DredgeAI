using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Storage;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace DredgeAI.BidCompare.TenderReadings;

/// <summary>P1 抽取编排：读取 IR → 运行三类抽取器 → Schema 校验 → 落库 BcBaselineFields / BcSourceMapItems → 更新任务状态。</summary>
public class BaselineExtractionService : ITransientDependency
{
    /// <summary>任务级进程内互斥：抽取整体「删旧重建」，并发执行会导致字段与锚点错配（多实例部署需换分布式锁）。</summary>
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> TaskGates = new();
    private readonly IRepository<BaselineField, Guid> _fieldRepository;
    private readonly IRepository<SourceMapItem, Guid> _sourceRepository;
    private readonly IRepository<TenderReadingTask, Guid> _taskRepository;
    private readonly IRepository<TenderReadingDocument, Guid> _documentRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IEnumerable<IBaselineFieldExtractor> _extractors;
    private readonly IBaselineSchemaValidator _schemaValidator;
    private readonly BaselineStore _baselineStore;
    private readonly ILogger<BaselineExtractionService> _logger;

    public BaselineExtractionService(
        IRepository<BaselineField, Guid> fieldRepository,
        IRepository<SourceMapItem, Guid> sourceRepository,
        IRepository<TenderReadingTask, Guid> taskRepository,
        IRepository<TenderReadingDocument, Guid> documentRepository,
        IFileStorage fileStorage,
        IEnumerable<IBaselineFieldExtractor> extractors,
        IBaselineSchemaValidator schemaValidator,
        BaselineStore baselineStore,
        ILogger<BaselineExtractionService> logger)
    {
        _fieldRepository = fieldRepository;
        _sourceRepository = sourceRepository;
        _taskRepository = taskRepository;
        _documentRepository = documentRepository;
        _fileStorage = fileStorage;
        _extractors = extractors;
        _schemaValidator = schemaValidator;
        _baselineStore = baselineStore;
        _logger = logger;
    }

    public async Task ReExtractCategoryAsync(
        Guid taskId,
        Guid documentId,
        BaselineCategory category,
        CancellationToken cancellationToken = default)
    {
        var gate = TaskGates.GetOrAdd(taskId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await ReExtractCategoryCoreAsync(taskId, documentId, category, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task ReExtractCategoryCoreAsync(
        Guid taskId,
        Guid documentId,
        BaselineCategory category,
        CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetAsync(taskId, cancellationToken: cancellationToken);
        var doc = await _documentRepository.FindAsync(documentId, cancellationToken: cancellationToken);
        if (doc == null || doc.IrStorageKey == null || doc.ParseStatus != DocumentParseStatus.Parsed)
        {
            task.MarkPartial($"文档 {documentId} 尚未解析完成，无法抽取");
            await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
            return;
        }

        string irJson;
        await using (var stream = await _fileStorage.GetAsync(doc.IrStorageKey, cancellationToken))
        using (var reader = new StreamReader(stream))
        {
            irJson = await reader.ReadToEndAsync(cancellationToken);
        }

        task.StartExtracting();
        await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);

        using var irDocument = JsonDocument.Parse(irJson);
        var context = new BaselineExtractionContext(taskId, irDocument.RootElement);

        // 只替换指定类别，保留其他类别字段
        var oldFields = await _fieldRepository.GetListAsync(
            f => f.TaskId == taskId && f.Category == category,
            cancellationToken: cancellationToken);
        if (oldFields.Count > 0)
        {
            var oldVersion = task.BaselineVersion;
            await _baselineStore.RemoveBaselineAsync(taskId, oldVersion, cancellationToken);
            task.BumpBaselineVersion();

            var oldFieldIds = oldFields.Select(f => f.Id).ToList();
            var oldSources = await _sourceRepository.GetListAsync(
                s => oldFieldIds.Contains(s.FieldId),
                cancellationToken: cancellationToken);
            await _sourceRepository.DeleteManyAsync(oldSources, autoSave: true, cancellationToken: cancellationToken);
            await _fieldRepository.DeleteManyAsync(oldFields, autoSave: true, cancellationToken: cancellationToken);
        }

        var extractor = _extractors.FirstOrDefault(e => e.Category == category);
        if (extractor == null)
        {
            task.MarkPartial($"未找到类别 {category} 的抽取器");
            await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
            return;
        }

        var savedCount = 0;
        var failureMessages = new List<string>();
        IReadOnlyList<BaselineFieldDraft> drafts;
        try
        {
            drafts = await extractor.ExtractAsync(context, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "抽取器 {Category} 执行失败", extractor.Category);
            task.MarkPartial($"{extractor.Category}: {ex.Message}");
            await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
            return;
        }

        foreach (var draft in drafts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var errors = _schemaValidator.Validate(extractor.Category, draft.FieldKey, draft.ValueJson);
            if (errors.Count > 0)
            {
                _logger.LogWarning("抽取结果校验失败 {Category}/{FieldKey}: {Errors}", extractor.Category, draft.FieldKey, string.Join("；", errors));
                failureMessages.Add($"{extractor.Category}/{draft.FieldKey}: {string.Join("；", errors)}");
                continue;
            }

            var field = new BaselineField(
                Guid.NewGuid(),
                taskId,
                extractor.Category,
                draft.FieldKey,
                draft.ValueJson,
                draft.RawText,
                draft.Confidence,
                draft.Status,
                draft.Extractor,
                draft.ExtractorVersion);
            await _fieldRepository.InsertAsync(field, autoSave: true, cancellationToken: cancellationToken);

            if (extractor.Category == BaselineCategory.ProjectInfo)
            {
                await ApplyProjectInfoAsync(task, draft, cancellationToken);
            }

            foreach (var sourceRef in draft.SourceRefs)
            {
                var bboxJson = JsonSerializer.Serialize(sourceRef.Bbox);
                await _sourceRepository.InsertAsync(
                    new SourceMapItem(
                        Guid.NewGuid(),
                        field.Id,
                        sourceRef.BlockId,
                        sourceRef.PageIdx,
                        bboxJson,
                        sourceRef.Text),
                    autoSave: true,
                    cancellationToken: cancellationToken);
            }

            savedCount++;
        }

        if (savedCount == 0)
        {
            task.MarkPartial(string.Join("；", failureMessages.DefaultIfEmpty($"类别 {category} 未抽取到任何字段")));
        }
        else if (failureMessages.Count > 0)
        {
            task.MarkPartial(string.Join("；", failureMessages));
        }
        else
        {
            task.MarkReady();
        }

        await _baselineStore.RemoveBaselineAsync(taskId, task.BaselineVersion, cancellationToken);
        await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
    }

    public async Task ExecuteAsync(Guid taskId, Guid documentId, CancellationToken cancellationToken = default)
    {
        var gate = TaskGates.GetOrAdd(taskId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await ExecuteCoreAsync(taskId, documentId, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task ExecuteCoreAsync(Guid taskId, Guid documentId, CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetAsync(taskId, cancellationToken: cancellationToken);
        var doc = await _documentRepository.FindAsync(documentId, cancellationToken: cancellationToken);
        if (doc == null || doc.IrStorageKey == null || doc.ParseStatus != DocumentParseStatus.Parsed)
        {
            task.MarkPartial($"文档 {documentId} 尚未解析完成，无法抽取");
            await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
            return;
        }

        string irJson;
        await using (var stream = await _fileStorage.GetAsync(doc.IrStorageKey, cancellationToken))
        using (var reader = new StreamReader(stream))
        {
            irJson = await reader.ReadToEndAsync(cancellationToken);
        }

        task.StartExtracting();
        await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);

        using var irDocument = JsonDocument.Parse(irJson);
        var context = new BaselineExtractionContext(taskId, irDocument.RootElement);

        _logger.LogInformation("读标抽取开始：任务 {TaskId}，抽取器数量 {Count}", taskId, _extractors.Count());

        // CountAsync(predicate) 是扩展方法，后台 Job 无环境 UoW 时会拿到已释放 DbContext；
        // 先取字段列表，用内存计数替代（下面删旧重建也要这份列表）
        var oldFields = await _fieldRepository.GetListAsync(f => f.TaskId == taskId, cancellationToken: cancellationToken);
        var oldVersion = task.BaselineVersion;
        if (oldFields.Count > 0)
        {
            await _baselineStore.RemoveBaselineAsync(taskId, oldVersion, cancellationToken);
            task.BumpBaselineVersion();
        }

        // 重抽时整体替换，避免残留旧字段/锚点
        if (oldFields.Count > 0)
        {
            var oldFieldIds = oldFields.Select(f => f.Id).ToList();
            var oldSources = await _sourceRepository.GetListAsync(
                s => oldFieldIds.Contains(s.FieldId),
                cancellationToken: cancellationToken);
            await _sourceRepository.DeleteManyAsync(oldSources, autoSave: true, cancellationToken: cancellationToken);
            await _fieldRepository.DeleteManyAsync(oldFields, autoSave: true, cancellationToken: cancellationToken);
        }

        var savedCount = 0;
        var failureMessages = new List<string>();
        foreach (var extractor in _extractors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<BaselineFieldDraft> drafts;
            try
            {
                drafts = await extractor.ExtractAsync(context, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "抽取器 {Category} 执行失败", extractor.Category);
                failureMessages.Add($"{extractor.Category}: {ex.Message}");
                continue;
            }

            _logger.LogInformation("抽取器 {Category} 产出 {Count} 个字段", extractor.Category, drafts.Count);

            foreach (var draft in drafts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var errors = _schemaValidator.Validate(extractor.Category, draft.FieldKey, draft.ValueJson);
                if (errors.Count > 0)
                {
                    _logger.LogWarning("抽取结果校验失败 {Category}/{FieldKey}: {Errors}", extractor.Category, draft.FieldKey, string.Join("；", errors));
                    failureMessages.Add($"{extractor.Category}/{draft.FieldKey}: {string.Join("；", errors)}");
                    continue;
                }

                var field = new BaselineField(
                    Guid.NewGuid(),
                    taskId,
                    extractor.Category,
                    draft.FieldKey,
                    draft.ValueJson,
                    draft.RawText,
                    draft.Confidence,
                    draft.Status,
                    draft.Extractor,
                    draft.ExtractorVersion);
                await _fieldRepository.InsertAsync(field, autoSave: true, cancellationToken: cancellationToken);

                if (extractor.Category == BaselineCategory.ProjectInfo)
                {
                    try
                    {
                        using var projectDoc = JsonDocument.Parse(draft.ValueJson);
                        if (projectDoc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String)
                        {
                            if (draft.FieldKey == "code")
                            {
                                task.SetProjectCode(value.GetString());
                            }
                            else if (draft.FieldKey == "name")
                            {
                                task.SetName(value.GetString()!);
                            }

                            await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
                        }
                    }
                    catch (JsonException)
                    {
                        // 不影响字段落库
                    }
                }

                foreach (var sourceRef in draft.SourceRefs)
                {
                    var bboxJson = JsonSerializer.Serialize(sourceRef.Bbox);
                    await _sourceRepository.InsertAsync(
                        new SourceMapItem(
                            Guid.NewGuid(),
                            field.Id,
                            sourceRef.BlockId,
                            sourceRef.PageIdx,
                            bboxJson,
                            sourceRef.Text),
                        autoSave: true,
                        cancellationToken: cancellationToken);
                }

                savedCount++;
            }
        }

        if (savedCount == 0)
        {
            task.MarkPartial(string.Join("；", failureMessages.DefaultIfEmpty("未抽取到任何基准库字段")));
        }
        else if (failureMessages.Count > 0)
        {
            task.MarkPartial(string.Join("；", failureMessages));
        }
        else
        {
            task.MarkReady();
        }

        // 首次抽取不会 bump 版本，必须显式失效可能已缓存的空基准库
        await _baselineStore.RemoveBaselineAsync(taskId, task.BaselineVersion, cancellationToken);
        await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
    }

    private async Task ApplyProjectInfoAsync(
        TenderReadingTask task,
        BaselineFieldDraft draft,
        CancellationToken cancellationToken)
    {
        try
        {
            using var projectDoc = JsonDocument.Parse(draft.ValueJson);
            if (projectDoc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String)
            {
                if (draft.FieldKey == "code")
                {
                    task.SetProjectCode(value.GetString());
                }
                else if (draft.FieldKey == "name")
                {
                    task.SetName(value.GetString()!);
                }

                await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
            }
        }
        catch (JsonException)
        {
            // 不影响字段落库
        }
    }
}
