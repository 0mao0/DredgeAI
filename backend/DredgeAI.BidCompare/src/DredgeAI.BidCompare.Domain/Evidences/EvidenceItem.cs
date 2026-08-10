using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.Evidences;

/// <summary>
/// 证据项（spec §3.2 核心数据结构）。DocIds/Locations/Metrics 以 JSON text 列存储，
/// 结构与 spec §6.1 一致：docIds[]、locations: { docId, blockIds[] }[]、metrics: { similarity? }。
/// </summary>
public class EvidenceItem : FullAuditedEntity<Guid>
{
    public Guid TaskId { get; private set; }

    public EvidenceType Type { get; private set; }

    public EvidenceSeverity Severity { get; private set; }

    public string DocIdsJson { get; private set; } = "[]";

    public string LocationsJson { get; private set; } = "[]";

    public string? MetricsJson { get; private set; }

    public string Title { get; private set; } = default!;

    public string Description { get; private set; } = default!;

    /// <summary>spec §3.2: 算法证据与 AI 结论在 UI 上可区分。</summary>
    public bool AiGenerated { get; private set; }

    protected EvidenceItem()
    {
    }

    public EvidenceItem(
        Guid id,
        Guid taskId,
        EvidenceType type,
        EvidenceSeverity severity,
        string docIdsJson,
        string locationsJson,
        string? metricsJson,
        string title,
        string description,
        bool aiGenerated) : base(id)
    {
        TaskId = taskId;
        Type = type;
        Severity = severity;
        DocIdsJson = Check.NotNullOrWhiteSpace(docIdsJson, nameof(docIdsJson));
        LocationsJson = Check.NotNullOrWhiteSpace(locationsJson, nameof(locationsJson));
        MetricsJson = metricsJson;
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), maxLength: 512);
        Description = Check.NotNullOrWhiteSpace(description, nameof(description), maxLength: 4000);
        AiGenerated = aiGenerated;
    }
}
