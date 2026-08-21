using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.TenderReadings.Extractors;

/// <summary>P1 章节框架抽取：直接取内部 IR outline，无需 LLM。</summary>
public class OutlineExtractor : IBaselineFieldExtractor, ITransientDependency
{
    public BaselineCategory Category => BaselineCategory.ChapterOutline;

    public Task<IReadOnlyList<BaselineFieldDraft>> ExtractAsync(
        BaselineExtractionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = context.IrRoot;
        var drafts = new List<BaselineFieldDraft>();

        if (!root.TryGetProperty("outline", out var outline) || outline.ValueKind != JsonValueKind.Array)
        {
            return Task.FromResult<IReadOnlyList<BaselineFieldDraft>>(drafts);
        }

        var outlineJson = JsonSerializer.Serialize(outline);
        var firstRef = FindFirstOutlineBlock(root, outline);

        drafts.Add(new BaselineFieldDraft
        {
            FieldKey = "outline",
            ValueJson = outlineJson,
            RawText = "目录树",
            Confidence = 0.99,
            Status = BaselineFieldStatus.Auto,
            Extractor = "rule",
            ExtractorVersion = "1.0",
            SourceRefs = ToDraftList(firstRef)
        });

        return Task.FromResult<IReadOnlyList<BaselineFieldDraft>>(drafts);
    }

    private static JsonElement? FindFirstOutlineBlock(JsonElement root, JsonElement outline)
    {
        if (outline.GetArrayLength() == 0)
        {
            return null;
        }

        var title = outline[0].TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
            ? t.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        if (!root.TryGetProperty("blocks", out var blocks) || blocks.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var block in blocks.EnumerateArray())
        {
            var text = block.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String
                ? textProp.GetString()
                : null;
            if (!string.IsNullOrWhiteSpace(text) && text!.Contains(title!, StringComparison.OrdinalIgnoreCase))
            {
                return block;
            }
        }

        return null;
    }

    private static List<SourceMapItemDraft> ToDraftList(JsonElement? block)
    {
        var list = new List<SourceMapItemDraft>();
        if (block == null)
        {
            return list;
        }

        list.Add(new SourceMapItemDraft
        {
            BlockId = GetString(block.Value, "blockId") ?? string.Empty,
            PageIdx = GetInt(block.Value, "pageIdx"),
            Bbox = GetBbox(block.Value),
            Text = GetString(block.Value, "text") ?? string.Empty
        });
        return list;
    }

    private static string? GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int GetInt(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;

    private static double[] GetBbox(JsonElement element)
    {
        if (!element.TryGetProperty("bbox", out var bbox)
            || bbox.ValueKind != JsonValueKind.Array
            || bbox.GetArrayLength() != 4)
        {
            return Array.Empty<double>();
        }

        var values = new double[4];
        var i = 0;
        foreach (var item in bbox.EnumerateArray())
        {
            values[i++] = item.ValueKind == JsonValueKind.Number ? item.GetDouble() : 0;
        }

        return values;
    }
}
