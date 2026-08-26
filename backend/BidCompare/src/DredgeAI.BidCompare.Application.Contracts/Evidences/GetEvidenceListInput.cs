using System;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.BidCompare.Evidences;

/// <summary>spec §6：按类型/严重度/文档对过滤。</summary>
public class GetEvidenceListInput : PagedResultRequestDto
{
    public EvidenceType? Type { get; set; }

    public EvidenceSeverity? Severity { get; set; }

    public Guid? DocIdA { get; set; }

    public Guid? DocIdB { get; set; }
}
