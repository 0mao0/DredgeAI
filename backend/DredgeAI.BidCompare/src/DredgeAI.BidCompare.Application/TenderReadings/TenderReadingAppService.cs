using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Exports;
using DredgeAI.BidCompare.Ir;
using DredgeAI.BidCompare.Storage;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Validation;

namespace DredgeAI.BidCompare.TenderReadings;

[RemoteService(false)] // 精确路由由 HttpApi 显式 Controller 暴露
public class TenderReadingAppService : ApplicationService, ITenderReadingAppService
{
    private static readonly string[] AllowedExtensions = { ".pdf", ".doc", ".docx" };

    /// <summary>Word→PDF 预览转换锁（按 previewKey 粒度串行化，用后移除）。</summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> PreviewConvertLocks = new();

    private static readonly JsonSerializerOptions OutlineJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IRepository<TenderReadingTask, Guid> _taskRepository;
    private readonly IRepository<TenderReadingDocument, Guid> _documentRepository;
    private readonly IRepository<BaselineField, Guid> _fieldRepository;
    private readonly IRepository<SourceMapItem, Guid> _sourceRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly BaselineStore _baselineStore;
    private readonly BaselineExtractionService _extractionService;
    private readonly IBaselineSchemaValidator _schemaValidator;
    private readonly IPdfConverter _pdfConverter;
    private readonly ILogger<TenderReadingAppService> _logger;

    public TenderReadingAppService(
        IRepository<TenderReadingTask, Guid> taskRepository,
        IRepository<TenderReadingDocument, Guid> documentRepository,
        IRepository<BaselineField, Guid> fieldRepository,
        IRepository<SourceMapItem, Guid> sourceRepository,
        IFileStorage fileStorage,
        IBackgroundJobManager backgroundJobManager,
        BaselineStore baselineStore,
        BaselineExtractionService extractionService,
        IBaselineSchemaValidator schemaValidator,
        IPdfConverter pdfConverter,
        ILogger<TenderReadingAppService> logger)
    {
        _taskRepository = taskRepository;
        _documentRepository = documentRepository;
        _fieldRepository = fieldRepository;
        _sourceRepository = sourceRepository;
        _fileStorage = fileStorage;
        _backgroundJobManager = backgroundJobManager;
        _baselineStore = baselineStore;
        _extractionService = extractionService;
        _schemaValidator = schemaValidator;
        _pdfConverter = pdfConverter;
        _logger = logger;
    }

    public async Task<TenderReadingTaskDto> CreateAsync(CreateTenderReadingTaskDto input)
    {
        var task = new TenderReadingTask(GuidGenerator.Create(), input.Name.Trim());
        task.SetProjectCode(input.ProjectCode);
        await _taskRepository.InsertAsync(task, autoSave: true);
        return await MapTaskDtoAsync(task);
    }

    public async Task<TenderReadingTaskDto> GetAsync(Guid id)
    {
        var task = await _taskRepository.GetAsync(id);
        return await MapTaskDtoAsync(task);
    }

    public async Task<PagedResultDto<TenderReadingTaskDto>> GetListAsync(GetTenderReadingTasksInput input)
    {
        var queryable = await _taskRepository.GetQueryableAsync();
        queryable = queryable
            .WhereIf(!input.Name.IsNullOrWhiteSpace(), x => x.Name.Contains(input.Name!))
            .WhereIf(input.Status.HasValue, x => x.Status == input.Status!.Value);

        var totalCount = await AsyncExecuter.CountAsync(queryable);
        var tasks = await AsyncExecuter.ToListAsync(queryable
            .OrderByDescending(x => x.CreationTime)
            .PageBy(input.SkipCount, input.MaxResultCount));

        // 批量取本页任务的文档，避免逐任务 N+1 查询
        var taskIds = tasks.Select(t => t.Id).ToList();
        var allDocuments = await _documentRepository.GetListAsync(d => taskIds.Contains(d.TaskId));

        var items = tasks
            .Select(task => MapTaskDto(task, allDocuments.Where(d => d.TaskId == task.Id).ToList()))
            .ToList();

        return new PagedResultDto<TenderReadingTaskDto>(totalCount, items);
    }

    public async Task<TenderReadingTaskDto> UpdateAsync(Guid id, UpdateTenderReadingTaskInput input)
    {
        var task = await _taskRepository.GetAsync(id);
        task.SetName(input.Name.Trim());
        task.SetProjectCode(input.ProjectCode);
        await _taskRepository.UpdateAsync(task, autoSave: true);
        return await MapTaskDtoAsync(task);
    }

    public async Task DeleteAsync(Guid id)
    {
        var task = await _taskRepository.GetAsync(id);
        await _baselineStore.RemoveBaselineAsync(id, task.BaselineVersion);
        var documents = await GetTaskDocumentsAsync(id);

        // 存储按任务前缀整树清理（含 raw/ 原始产物、preview.pdf 等孤儿对象）
        await DeleteStoragePrefixQuietlyAsync($"tender-read/{id}/");

        var fields = await _fieldRepository.GetListAsync(f => f.TaskId == id);
        if (fields.Count > 0)
        {
            var fieldIds = fields.Select(f => f.Id).ToList();
            var sources = await _sourceRepository.GetListAsync(s => fieldIds.Contains(s.FieldId));
            await _sourceRepository.DeleteManyAsync(sources, autoSave: true);
            await _fieldRepository.DeleteManyAsync(fields, autoSave: true);
        }

        await _documentRepository.DeleteManyAsync(documents, autoSave: true);
        await _taskRepository.DeleteAsync(task, autoSave: true);
    }

    [DisableValidation] // Stream 参数无法被验证拦截器递归校验
    public async Task<TenderReadingDocumentDto> UploadDocumentAsync(Guid id, string fileName, Stream content)
    {
        var task = await _taskRepository.GetAsync(id);
        if (task.Status is TenderReadingTaskStatus.Parsing
            or TenderReadingTaskStatus.Extracting
            or TenderReadingTaskStatus.Ready
            or TenderReadingTaskStatus.Reviewing)
        {
            throw new BusinessException(TenderReadErrorCodes.InvalidTaskState)
                .WithData("action", "UploadDocument")
                .WithData("status", task.Status.ToString())
                .WithData("reason", "当前任务状态不允许继续上传文件");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            throw new BusinessException(TenderReadErrorCodes.UnsupportedFileType)
                .WithData("extension", extension);
        }

        var documentId = GuidGenerator.Create();
        var storageKey = $"tender-read/{id}/{documentId}/origin{extension}";
        var header = new byte[8];
        var headerLength = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        if (!UploadFileSignature.Matches(extension, header.AsSpan(0, headerLength)))
        {
            _logger.LogWarning(
                "读标上传文件 {FileName}（扩展名 {Extension}）内容与扩展名不符，已按内容继续处理",
                fileName,
                extension);
        }

        var uploadStream = new PrefixCountingStream(header, headerLength, content);
        await _fileStorage.UploadAsync(storageKey, uploadStream, ContentTypeOf(extension));

        var document = new TenderReadingDocument(
            documentId,
            id,
            Path.GetFileName(fileName),
            uploadStream.TotalBytesRead,
            storageKey);
        await _documentRepository.InsertAsync(document, autoSave: true);

        return MapDocumentDto(document);
    }

    public async Task<List<TenderReadingDocumentDto>> GetDocumentsAsync(Guid id)
    {
        await _taskRepository.GetAsync(id);
        var documents = await GetTaskDocumentsAsync(id);
        return documents.OrderBy(d => d.CreationTime).Select(MapDocumentDto).ToList();
    }

    public async Task<TenderReadingTaskDto> StartParsingAsync(Guid id)
    {
        var task = await _taskRepository.GetAsync(id);
        var documents = await GetTaskDocumentsAsync(id);

        if (documents.Count == 0)
        {
            throw new BusinessException(TenderReadErrorCodes.InvalidTaskState)
                .WithData("action", "StartParsing")
                .WithData("reason", "尚未上传招标文件");
        }

        var pending = documents
            .Where(d => d.ParseStatus is DocumentParseStatus.Pending or DocumentParseStatus.Failed)
            .ToList();
        if (pending.Count == 0)
        {
            throw new BusinessException(TenderReadErrorCodes.InvalidTaskState)
                .WithData("action", "StartParsing")
                .WithData("reason", "没有待解析文档");
        }

        if (documents.Any(d => d.ParseStatus == DocumentParseStatus.Parsing))
        {
            throw new BusinessException(TenderReadErrorCodes.InvalidTaskState)
                .WithData("action", "StartParsing")
                .WithData("reason", "解析进行中，请勿重复触发");
        }

        task.StartParsing();
        await _taskRepository.UpdateAsync(task, autoSave: true);

        foreach (var document in pending)
        {
            if (document.ParseStatus == DocumentParseStatus.Failed)
            {
                document.MarkPendingForReparse();
                await _documentRepository.UpdateAsync(document, autoSave: true);
            }

            await _backgroundJobManager.EnqueueAsync(new ParseTenderDocumentArgs
            {
                TaskId = id,
                DocumentId = document.Id
            });
        }

        return await MapTaskDtoAsync(task);
    }

    public async Task<TenderReadingTaskDto> ReparseAsync(Guid id)
    {
        var task = await _taskRepository.GetAsync(id);
        var documents = await GetTaskDocumentsAsync(id);

        var failed = documents.Where(d => d.ParseStatus == DocumentParseStatus.Failed).ToList();
        if (failed.Count == 0)
        {
            throw new BusinessException(TenderReadErrorCodes.InvalidTaskState)
                .WithData("action", "Reparse")
                .WithData("reason", "无失败文档");
        }

        if (documents.Any(d => d.ParseStatus == DocumentParseStatus.Parsing))
        {
            throw new BusinessException(TenderReadErrorCodes.InvalidTaskState)
                .WithData("action", "Reparse")
                .WithData("reason", "解析进行中，请勿重复触发");
        }

        task.StartParsing();
        await _taskRepository.UpdateAsync(task, autoSave: true);

        foreach (var document in failed)
        {
            document.MarkPendingForReparse();
            await _documentRepository.UpdateAsync(document, autoSave: true);
            await _backgroundJobManager.EnqueueAsync(new ParseTenderDocumentArgs
            {
                TaskId = id,
                DocumentId = document.Id
            });
        }

        return await MapTaskDtoAsync(task);
    }

    public async Task<List<TenderReadingOutlineNodeDto>> GetOutlineAsync(Guid id)
    {
        // 解析尚未完成时目录不可用是常态（前端轮询），返回空列表而非 403，
        // 避免把「还没好」当错误处理产生持续错误提示
        var document = await GetParsedDocumentIfReadyAsync(id);
        if (document == null)
        {
            return new List<TenderReadingOutlineNodeDto>();
        }

        await using var stream = await _fileStorage.GetAsync(document.IrStorageKey!);
        using var ir = await JsonDocument.ParseAsync(stream);

        if (!ir.RootElement.TryGetProperty("outline", out var outline)
            || outline.ValueKind != JsonValueKind.Array)
        {
            return new List<TenderReadingOutlineNodeDto>();
        }

        return JsonSerializer.Deserialize<List<TenderReadingOutlineNodeDto>>(outline.GetRawText(), OutlineJsonOptions) ?? new();
    }

    public async Task<TenderReadingParsedDocumentDto> GetParsedDocumentAsync(Guid id)
    {
        // 解析未完成时返回空产物，前端轮询到状态就绪后再拉取
        var document = await GetParsedDocumentIfReadyAsync(id);
        if (document == null)
        {
            return new TenderReadingParsedDocumentDto();
        }

        string irJson;
        await using (var irStream = await _fileStorage.GetAsync(document.IrStorageKey!))
        using (var reader = new StreamReader(irStream))
        {
            irJson = await reader.ReadToEndAsync();
        }

        var ir = JsonSerializer.Deserialize<DocumentIrDto>(irJson, OutlineJsonOptions) ?? new DocumentIrDto();

        string content;
        if (!string.IsNullOrWhiteSpace(document.DocMdStorageKey)
            && await _fileStorage.ExistsAsync(document.DocMdStorageKey))
        {
            await using var mdStream = await _fileStorage.GetAsync(document.DocMdStorageKey);
            using var mdReader = new StreamReader(mdStream);
            content = await mdReader.ReadToEndAsync();
        }
        else
        {
            content = BuildMarkdownFromIr(ir);
        }

        return new TenderReadingParsedDocumentDto
        {
            Content = content,
            Ir = ir
        };
    }

    public async Task<TenderReadingBaselineDto> GetBaselineAsync(Guid id)
    {
        var task = await _taskRepository.GetAsync(id);
        return await _baselineStore.GetBaselineAsync(id, task.BaselineVersion);
    }

    public async Task<List<BaselineFieldDto>> GetBaselineByCategoryAsync(Guid id, BaselineCategory category)
    {
        await _taskRepository.GetAsync(id);
        var fields = await _fieldRepository.GetListAsync(f => f.TaskId == id && f.Category == category);
        fields = fields.OrderBy(f => f.FieldKey).ToList();
        return await MapFieldDtosAsync(fields);
    }

    public async Task<List<SourceRefDto>> GetSourceAsync(Guid id, Guid fieldId)
    {
        await _taskRepository.GetAsync(id);
        var field = await _fieldRepository.FirstOrDefaultAsync(f => f.TaskId == id && f.Id == fieldId);
        if (field == null)
        {
            throw new BusinessException(TenderReadErrorCodes.FieldNotFound)
                .WithData("fieldId", fieldId);
        }

        return await _baselineStore.GetSourceRefsAsync(fieldId);
    }

    public async Task<BaselineFieldDto> UpdateFieldAsync(Guid id, Guid fieldId, UpdateBaselineFieldInput input)
    {
        var task = await _taskRepository.GetAsync(id);
        var field = await _fieldRepository.FirstOrDefaultAsync(f => f.TaskId == id && f.Id == fieldId);
        if (field == null)
        {
            throw new BusinessException(TenderReadErrorCodes.FieldNotFound)
                .WithData("fieldId", fieldId);
        }

        if (input.Status is not (BaselineFieldStatus.Confirmed or BaselineFieldStatus.Edited))
        {
            throw new BusinessException(TenderReadErrorCodes.InvalidTaskState)
                .WithData("action", "UpdateField")
                .WithData("reason", "人工更新字段状态必须为 confirmed 或 edited");
        }

        var errors = _schemaValidator.Validate(field.Category, field.FieldKey, input.ValueJson);
        if (errors.Count > 0)
        {
            throw new BusinessException(TenderReadErrorCodes.IrValidationFailed)
                .WithData("errors", string.Join("；", errors));
        }

        var confidence = input.Confidence ?? field.Confidence;
        field.UpdateByHuman(input.ValueJson, input.RawText, confidence, input.Status);
        await _fieldRepository.UpdateAsync(field, autoSave: true);

        await _baselineStore.RemoveBaselineAsync(id, task.BaselineVersion);

        var sources = await _sourceRepository.GetListAsync(s => s.FieldId == fieldId);
        return MapFieldDto(field, sources);
    }

    /// <summary>重抽基准库：LLM 抽取最坏数分钟，转后台任务执行，前端轮询任务状态感知完成。</summary>
    public async Task<TenderReadingTaskDto> ReExtractAsync(Guid id, ReExtractBaselineInput input)
    {
        var task = await _taskRepository.GetAsync(id);
        // 进行中互斥：解析/抽取中不允许重抽（并发重抽会并发「删旧重建」导致字段错配）
        if (task.Status is TenderReadingTaskStatus.Uploading
            or TenderReadingTaskStatus.Parsing
            or TenderReadingTaskStatus.Extracting)
        {
            throw new BusinessException(TenderReadErrorCodes.InvalidTaskState)
                .WithData("action", "ReExtract")
                .WithData("status", task.Status.ToString());
        }
        var document = await GetParsedDocumentOrThrowAsync(id);

        task.StartExtracting();
        try
        {
            await _taskRepository.UpdateAsync(task, autoSave: true);
        }
        catch (Exception ex) when (DbConcurrency.IsConflict(ex))
        {
            throw DbConcurrency.ToInvalidState("ReExtract");
        }

        await _backgroundJobManager.EnqueueAsync(new ReExtractBaselineArgs
        {
            TaskId = id,
            DocumentId = document.Id,
            Category = input.Category
        });

        return await MapTaskDtoAsync(task);
    }

    public async Task<TenderReadingDocumentFileResult> GetDocumentFileAsync(Guid id)
    {
        await _taskRepository.GetAsync(id);
        var documents = await GetTaskDocumentsAsync(id);
        var document = documents.OrderBy(d => d.CreationTime).FirstOrDefault();
        if (document == null)
        {
            throw new BusinessException(TenderReadErrorCodes.DocumentNotFound)
                .WithData("taskId", id);
        }

        var extension = Path.GetExtension(document.FileName).ToLowerInvariant();
        // Word 文档在线预览：首次请求用 LibreOffice 转 PDF 并缓存，之后直接返回 PDF。
        if (extension is ".doc" or ".docx")
        {
            var previewKey = $"tender-read/{document.TaskId}/{document.Id}/preview.pdf";
            // 并发首访串行化（与比标预览同一模式），避免重复转换；锁用后移除防字典膨胀
            var convertLock = PreviewConvertLocks.GetOrAdd(previewKey, _ => new SemaphoreSlim(1, 1));
            await convertLock.WaitAsync();
            try
            {
                if (await _fileStorage.ExistsAsync(previewKey))
                {
                    return new TenderReadingDocumentFileResult
                    {
                        Content = await _fileStorage.GetAsync(previewKey),
                        ContentType = "application/pdf",
                        FileName = Path.GetFileNameWithoutExtension(document.FileName) + ".pdf"
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
                    return new TenderReadingDocumentFileResult
                    {
                        Content = new MemoryStream(pdfBytes),
                        ContentType = "application/pdf",
                        FileName = Path.GetFileNameWithoutExtension(document.FileName) + ".pdf"
                    };
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "读标 Word 文档转 PDF 预览失败，回退返回原始文件：{Key}", document.OriginStorageKey);
                }
            }
            finally
            {
                convertLock.Release();
                if (convertLock.CurrentCount == 1)
                {
                    PreviewConvertLocks.TryRemove(previewKey, out _);
                }
            }
        }

        var content = await _fileStorage.GetAsync(document.OriginStorageKey);
        return new TenderReadingDocumentFileResult
        {
            Content = content,
            ContentType = ContentTypeOf(extension),
            FileName = document.FileName
        };
    }

    public async Task<TenderReadingBaselineDto> ExportBaselineAsync(Guid id)
    {
        return await GetBaselineAsync(id);
    }

    private async Task<TenderReadingDocument> GetParsedDocumentOrThrowAsync(Guid taskId)
    {
        var document = await GetParsedDocumentIfReadyAsync(taskId);
        if (document == null)
        {
            throw new BusinessException(TenderReadErrorCodes.DocumentNotParsed)
                .WithData("taskId", taskId);
        }

        return document;
    }

    /// <summary>取第一个已解析且有 IR 的文档；未就绪返回 null（读接口据此给空态而非报错）。</summary>
    private async Task<TenderReadingDocument?> GetParsedDocumentIfReadyAsync(Guid taskId)
    {
        await _taskRepository.GetAsync(taskId);
        var documents = await GetTaskDocumentsAsync(taskId);
        return documents
            .Where(d => d.ParseStatus == DocumentParseStatus.Parsed && d.IrStorageKey != null)
            .OrderBy(d => d.CreationTime)
            .FirstOrDefault();
    }

    private async Task<List<TenderReadingDocument>> GetTaskDocumentsAsync(Guid taskId)
    {
        return await _documentRepository.GetListAsync(d => d.TaskId == taskId);
    }

    private async Task<List<BaselineFieldDto>> MapFieldDtosAsync(List<BaselineField> fields)
    {
        if (fields.Count == 0)
        {
            return new List<BaselineFieldDto>();
        }

        var fieldIds = fields.Select(f => f.Id).ToList();
        var sources = await _sourceRepository.GetListAsync(s => fieldIds.Contains(s.FieldId));
        return fields.Select(f => MapFieldDto(f, sources.Where(s => s.FieldId == f.Id).ToList())).ToList();
    }

    private BaselineFieldDto MapFieldDto(BaselineField field, List<SourceMapItem> sources)
    {
        return new BaselineFieldDto
        {
            Id = field.Id,
            TaskId = field.TaskId,
            Category = field.Category,
            FieldKey = field.FieldKey,
            ValueJson = field.ValueJson,
            RawText = field.RawText,
            Confidence = field.Confidence,
            Status = field.Status,
            Extractor = field.Extractor,
            ExtractorVersion = field.ExtractorVersion,
            SourceRefs = sources
                .OrderBy(s => s.PageIdx)
                .Select(s => new SourceRefDto
                {
                    FieldId = s.FieldId,
                    BlockId = s.BlockId,
                    PageIdx = s.PageIdx,
                    Bbox = ParseBbox(s.BboxJson),
                    Text = s.Text
                })
                .ToList()
        };
    }

    private async Task<TenderReadingTaskDto> MapTaskDtoAsync(TenderReadingTask task)
        => MapTaskDto(task, await GetTaskDocumentsAsync(task.Id));

    private static TenderReadingTaskDto MapTaskDto(TenderReadingTask task, List<TenderReadingDocument> documents)
    {
        return new TenderReadingTaskDto
        {
            Id = task.Id,
            Name = task.Name,
            ProjectCode = task.ProjectCode,
            Status = task.Status,
            ProgressStage = task.ProgressStage,
            ProgressPercent = task.ProgressPercent,
            BaselineVersion = task.BaselineVersion,
            FailureReason = task.FailureReason,
            DocIds = documents.OrderBy(d => d.CreationTime).Select(d => d.Id).ToList(),
            CreatedAt = task.CreationTime
        };
    }

    private static TenderReadingDocumentDto MapDocumentDto(TenderReadingDocument document)
    {
        return new TenderReadingDocumentDto
        {
            Id = document.Id,
            TaskId = document.TaskId,
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
            CreatedAt = document.CreationTime
        };
    }

    private async Task DeleteStoragePrefixQuietlyAsync(string prefix)
    {
        try
        {
            await _fileStorage.DeleteByPrefixAsync(prefix);
        }
        catch
        {
            // 删除失败不阻塞任务删除
        }
    }

    private static double[] ParseBbox(string bboxJson)
    {
        try
        {
            return JsonSerializer.Deserialize<double[]>(bboxJson) ?? Array.Empty<double>();
        }
        catch (JsonException)
        {
            return Array.Empty<double>();
        }
    }

    /// <summary>AnGIneer 未产出 content.md 时，用 IR 块重建一份可读 Markdown（页眉/页脚忽略）。</summary>
    private static string BuildMarkdownFromIr(DocumentIrDto ir)
    {
        var sb = new StringBuilder();
        foreach (var block in ir.Blocks)
        {
            if (block.Type is "header" or "footer")
            {
                continue;
            }

            var text = block.Text?.Trim() ?? string.Empty;
            switch (block.Type)
            {
                case "title":
                    var level = Math.Clamp(block.TextLevel > 0 ? block.TextLevel : 1, 1, 6);
                    if (text.Length > 0)
                    {
                        sb.Append('#', level).Append(' ').AppendLine(text);
                    }
                    break;
                case "equation":
                    if (text.Length > 0)
                    {
                        sb.AppendLine("$$").AppendLine(text).AppendLine("$$");
                    }
                    break;
                case "image":
                    sb.AppendLine(text.Length > 0 ? $"[图片] {text}" : "[图片]");
                    break;
                case "table":
                    sb.AppendLine(block.Table?.Html ?? (text.Length > 0 ? text : "[表格]"));
                    break;
                default:
                    if (text.Length > 0)
                    {
                        sb.AppendLine(text);
                    }
                    break;
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string ContentTypeOf(string extension) => extension switch
    {
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        _ => "application/octet-stream"
    };
}
