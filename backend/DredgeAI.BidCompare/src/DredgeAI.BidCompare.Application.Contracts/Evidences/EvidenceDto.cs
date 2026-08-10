using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.BidCompare.Evidences;

/// <summary>spec §6.1 Evidence：id, taskId, type, severity, docIds, locations, metrics, title, description, aiGenerated。</summary>
public class EvidenceDto : EntityDto<Guid>
{
    public Guid TaskId { get; set; }

    public EvidenceType Type { get; set; }

    public EvidenceSeverity Severity { get; set; }

    public List<Guid> DocIds { get; set; } = new();

    public List<EvidenceLocationDto> Locations { get; set; } = new();

    public EvidenceMetricsDto? Metrics { get; set; }

    public string Title { get; set; } = default!;

    public string Description { get; set; } = default!;

    public bool AiGenerated { get; set; }
}
