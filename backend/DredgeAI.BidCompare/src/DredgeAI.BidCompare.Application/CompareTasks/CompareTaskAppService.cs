using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Analysis;
using DredgeAI.BidCompare.BackgroundJobs;
using DredgeAI.BidCompare.Clauses;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Evidences;
using DredgeAI.BidCompare.Ir;
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
    private const int MaxBidDocuments = 5;

    internal static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IRepository<CompareTask, Guid> _taskRepository;
    private readonly IRepository<CompareDocument, Guid> _documentRepository;
    private readonly IRepository<EvidenceItem, Guid> _evidenceRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public CompareTaskAppService(
        IRepository<CompareTask, Guid> taskRepository,
        IRepository<CompareDocument, Guid> documentRepository,
        IRepository<EvidenceItem, Guid> evidenceRepository,
        IFileStorage fileStorage,
        IBackgroundJobManager backgroundJobManager)
    {
        _taskRepository = taskRepository;
        _documentRepository = documentRepository;
        _evidenceRepository = evidenceRepository;
        _fileStorage = fileStorage;
        _backgroundJobManager = backgroundJobManager;
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

        await _backgroundJobManager.EnqueueAsync(new ParseDocumentArgs { TaskId = id, DocumentId = documentId });

        return MapToDto(document);
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
        return new CompareTaskDto
        {
            Id = task.Id,
            Name = task.Name,
            Status = task.Status,
            DocIds = documents.OrderBy(d => d.CreationTime).Select(d => d.Id).ToList(),
            TenderDocId = task.TenderDocumentId,
            ClauseSnapshot = task.ClauseSnapshotJson == null
                ? null
                : JsonSerializer.Deserialize<List<ClauseDto>>(task.ClauseSnapshotJson, SnapshotJsonOptions),
            Progress = new CompareProgressDto
            {
                Stage = task.ProgressStage,
                Percent = task.ProgressPercent,
                Message = task.ProgressMessage
            },
            CreatedAt = task.CreationTime
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
