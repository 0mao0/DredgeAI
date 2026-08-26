using System;
using DredgeAI.BidCompare.Documents;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.Drafts;

/// <summary>
/// 上传会话中的暂存文件：选中即上传，仅存储、不建任务、不触发解析；
/// 用户点「开始分析」后由任务创建流程转正为 CompareDocument。
/// </summary>
public class CompareDraftDocument : FullAuditedEntity<Guid>
{
    /// <summary>上传会话 ID（前端生成 UUID，不属于任务记录）。</summary>
    public Guid DraftId { get; private set; }

    public DocumentRole Role { get; private set; }

    public string FileName { get; private set; } = default!;

    public string FileExtension { get; private set; } = default!;

    public long FileSize { get; private set; }

    /// <summary>原始文件对象存储 key：compare/drafts/{draftId}/{docId}/origin.{ext}。</summary>
    public string OriginStorageKey { get; private set; } = default!;

    protected CompareDraftDocument()
    {
    }

    public CompareDraftDocument(
        Guid id,
        Guid draftId,
        DocumentRole role,
        string fileName,
        long fileSize,
        string originStorageKey) : base(id)
    {
        DraftId = draftId;
        Role = role;
        FileName = Check.NotNullOrWhiteSpace(fileName, nameof(fileName), maxLength: 256);
        FileExtension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        FileSize = fileSize;
        OriginStorageKey = Check.NotNullOrWhiteSpace(originStorageKey, nameof(originStorageKey), maxLength: 512);
    }
}
