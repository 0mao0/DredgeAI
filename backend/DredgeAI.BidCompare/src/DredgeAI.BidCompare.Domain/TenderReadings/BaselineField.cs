using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.TenderReadings;

/// <summary>基准库字段（P1：项目信息、商务数据、目录树三类）。</summary>
public class BaselineField : FullAuditedEntity<Guid>
{
    public Guid TaskId { get; private set; }

    public BaselineCategory Category { get; private set; }

    /// <summary>业务字段名，如 price_ceiling。</summary>
    public string FieldKey { get; private set; } = default!;

    /// <summary>结构化值 JSON。</summary>
    public string ValueJson { get; private set; } = default!;

    /// <summary>原文摘要。</summary>
    public string RawText { get; private set; } = default!;

    /// <summary>置信度 0~1。</summary>
    public double Confidence { get; private set; }

    public BaselineFieldStatus Status { get; private set; }

    /// <summary>来源：rule / llm / rule+llm。</summary>
    public string Extractor { get; private set; } = default!;

    /// <summary>抽取器版本。</summary>
    public string ExtractorVersion { get; private set; } = default!;

    protected BaselineField()
    {
    }

    /// <summary>人工确认或修改字段：状态必须是 Confirmed 或 Edited。</summary>
    public void UpdateByHuman(string valueJson, string? rawText, double confidence, BaselineFieldStatus status)
    {
        if (status is not (BaselineFieldStatus.Confirmed or BaselineFieldStatus.Edited))
        {
            throw new ArgumentException("人工更新字段状态必须为 Confirmed 或 Edited", nameof(status));
        }

        ValueJson = Check.NotNullOrWhiteSpace(valueJson, nameof(valueJson));
        RawText = string.IsNullOrWhiteSpace(rawText) ? string.Empty : rawText.Trim();
        if (RawText.Length > 4000)
        {
            RawText = RawText[..4000];
        }
        Confidence = Math.Clamp(confidence, 0, 1);
        Status = status;
    }

    public BaselineField(
        Guid id,
        Guid taskId,
        BaselineCategory category,
        string fieldKey,
        string valueJson,
        string rawText,
        double confidence,
        BaselineFieldStatus status,
        string extractor,
        string extractorVersion) : base(id)
    {
        TaskId = taskId;
        Category = category;
        FieldKey = Check.NotNullOrWhiteSpace(fieldKey, nameof(fieldKey), maxLength: 128);
        ValueJson = Check.NotNullOrWhiteSpace(valueJson, nameof(valueJson));
        RawText = rawText ?? string.Empty;
        if (RawText.Length > 4000)
        {
            RawText = RawText[..4000];
        }
        Confidence = Math.Clamp(confidence, 0, 1);
        Status = status;
        Extractor = Check.NotNullOrWhiteSpace(extractor, nameof(extractor), maxLength: 32);
        ExtractorVersion = Check.NotNullOrWhiteSpace(extractorVersion, nameof(extractorVersion), maxLength: 32);
    }
}
