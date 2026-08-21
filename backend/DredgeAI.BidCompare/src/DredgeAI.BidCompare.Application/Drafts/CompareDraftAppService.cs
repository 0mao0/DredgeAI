using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Storage;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Validation;

namespace DredgeAI.BidCompare.Drafts;

[RemoteService(false)] // 精确路由由 HttpApi 显式 Controller 暴露
public class CompareDraftAppService : ApplicationService, ICompareDraftAppService
{
    private static readonly string[] AllowedExtensions = { ".pdf", ".doc", ".docx" };
    private const int MaxBidDocuments = 8;

    private readonly IRepository<CompareDraftDocument, Guid> _draftDocumentRepository;
    private readonly IFileStorage _fileStorage;

    public CompareDraftAppService(
        IRepository<CompareDraftDocument, Guid> draftDocumentRepository,
        IFileStorage fileStorage)
    {
        _draftDocumentRepository = draftDocumentRepository;
        _fileStorage = fileStorage;
    }

    public async Task<List<CompareDraftDocumentDto>> GetDocumentsAsync(Guid draftId)
    {
        var queryable = await _draftDocumentRepository.GetQueryableAsync();
        var docs = await AsyncExecuter.ToListAsync(queryable
            .Where(d => d.DraftId == draftId)
            .OrderBy(d => d.CreationTime));
        return docs.Select(MapToDto).ToList();
    }

    [DisableValidation]
    public async Task<CompareDraftDocumentDto> UploadDocumentAsync(
        Guid draftId,
        DocumentRole role,
        string fileName,
        Stream content)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            throw new BusinessException(BidCompareErrorCodes.UnsupportedFileType)
                .WithData("extension", extension);
        }

        var queryable = await _draftDocumentRepository.GetQueryableAsync();
        var bidCount = await AsyncExecuter.CountAsync(
            queryable.Where(d => d.DraftId == draftId && d.Role == DocumentRole.Bid));
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
        var storageKey = $"compare/drafts/{draftId}/{documentId}/origin{extension}";
        var uploadStream = new PrefixCountingStream(header, headerLength, content);
        await _fileStorage.UploadAsync(storageKey, uploadStream, ContentTypeOf(extension));

        var document = new CompareDraftDocument(
            documentId,
            draftId,
            role,
            Path.GetFileName(fileName),
            uploadStream.TotalBytesRead,
            storageKey);
        await _draftDocumentRepository.InsertAsync(document, autoSave: true);
        return MapToDto(document);
    }

    public async Task DeleteDocumentAsync(Guid draftId, Guid docId)
    {
        var document = await _draftDocumentRepository.FirstOrDefaultAsync(
            d => d.DraftId == draftId && d.Id == docId);
        if (document == null)
        {
            return; // 幂等：重复删�?越权按成功处理（不暴露归属信息）
        }

        await DeleteStorageQuietlyAsync(document.OriginStorageKey);
        await _draftDocumentRepository.DeleteAsync(document, autoSave: true);
    }

    public async Task DeleteDraftAsync(Guid draftId)
    {
        var queryable = await _draftDocumentRepository.GetQueryableAsync();
        var documents = await AsyncExecuter.ToListAsync(
            queryable.Where(d => d.DraftId == draftId));

        // 按会话前缀整树清理（逐 key 删除在部分失败时会留孤儿对象）
        try
        {
            await _fileStorage.DeleteByPrefixAsync($"compare/drafts/{draftId}/");
        }
        catch
        {
            // 对象存储删除失败不阻塞会话删除（孤儿对象由运维清理）
        }

        await _draftDocumentRepository.DeleteManyAsync(documents, autoSave: true);
    }

    private static CompareDraftDocumentDto MapToDto(CompareDraftDocument document) => new()
    {
        Id = document.Id,
        DraftId = document.DraftId,
        Role = document.Role,
        FileName = document.FileName,
        FileSize = document.FileSize,
        CreatedAt = document.CreationTime
    };

    private async Task DeleteStorageQuietlyAsync(string key)
    {
        try
        {
            await _fileStorage.DeleteAsync(key);
        }
        catch
        {
            // 对象存储删除失败不阻塞（孤儿对象由运维清理）
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
