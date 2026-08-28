using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.TenderReadings;

/// <summary>抽取上下文：任务 ID + 已校验的内部适配 IR 根节点。</summary>
public sealed class BaselineExtractionContext
{
    public Guid TaskId { get; }

    public JsonElement IrRoot { get; }

    public BaselineExtractionContext(Guid taskId, JsonElement irRoot)
    {
        TaskId = taskId;
        IrRoot = irRoot;
    }
}

/// <summary>抽取器产出的字段草稿（未落库）。</summary>
public sealed class BaselineFieldDraft
{
    public string FieldKey { get; init; } = default!;

    public string ValueJson { get; init; } = default!;

    public string RawText { get; init; } = string.Empty;

    public double Confidence { get; init; }

    public BaselineFieldStatus Status { get; init; } = BaselineFieldStatus.Auto;

    public string Extractor { get; init; } = "rule";

    public string ExtractorVersion { get; init; } = "1.0";

    public List<SourceMapItemDraft> SourceRefs { get; init; } = new();
}

/// <summary>源锚点草稿。</summary>
public sealed class SourceMapItemDraft
{
    public string BlockId { get; init; } = default!;

    public int PageIdx { get; init; }

    public double[] Bbox { get; init; } = System.Array.Empty<double>();

    public string Text { get; init; } = string.Empty;
}

/// <summary>基准库字段抽取器接口。</summary>
public interface IBaselineFieldExtractor
{
    BaselineCategory Category { get; }

    Task<IReadOnlyList<BaselineFieldDraft>> ExtractAsync(BaselineExtractionContext context, CancellationToken cancellationToken = default);
}
