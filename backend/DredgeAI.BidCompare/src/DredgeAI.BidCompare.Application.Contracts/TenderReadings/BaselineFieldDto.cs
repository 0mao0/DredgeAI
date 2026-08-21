using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.BidCompare.TenderReadings;

public class BaselineFieldDto : EntityDto<Guid>
{
    public Guid TaskId { get; set; }

    public BaselineCategory Category { get; set; }

    public string FieldKey { get; set; } = default!;

    /// <summary>结构化值 JSON（字符串，前端可 JSON.parse；后续可升级为对象字段）。</summary>
    public string ValueJson { get; set; } = default!;

    public string RawText { get; set; } = default!;

    public double Confidence { get; set; }

    public BaselineFieldStatus Status { get; set; }

    public string Extractor { get; set; } = default!;

    public string ExtractorVersion { get; set; } = default!;

    public List<SourceRefDto> SourceRefs { get; set; } = new();
}
