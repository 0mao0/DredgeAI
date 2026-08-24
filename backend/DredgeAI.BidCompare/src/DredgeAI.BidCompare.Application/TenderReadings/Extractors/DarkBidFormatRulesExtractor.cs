using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.TenderReadings.Extractors;

/// <summary>P3 暗标格式规则抽取：关键词匹配匿名、字体、页数等暗标要求。</summary>
public class DarkBidFormatRulesExtractor : IBaselineFieldExtractor, ITransientDependency
{
    private static readonly string[] Keywords = { "暗标", "匿名", "不得出现", "不得标明", "字体", "页数", "密封" };

    public BaselineCategory Category => BaselineCategory.DarkBidFormatRules;

    public Task<IReadOnlyList<BaselineFieldDraft>> ExtractAsync(
        BaselineExtractionContext context,
        CancellationToken cancellationToken = default)
    {
        var drafts = new List<BaselineFieldDraft>();
        var index = 0;

        foreach (var block in EnumerateBlocks(context.IrRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = GetString(block, "text");
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var hit = false;
            foreach (var keyword in Keywords)
            {
                if (text!.Contains(keyword, StringComparison.Ordinal))
                {
                    hit = true;
                    break;
                }
            }
            if (!hit)
            {
                continue;
            }

            index++;
            var raw = text.Trim();
            drafts.Add(new BaselineFieldDraft
            {
                FieldKey = $"dark_bid_rule_{index}",
                ValueJson = JsonSerializer.Serialize(new { value = raw }),
                RawText = raw,
                Confidence = 0.88,
                Status = BaselineFieldStatus.Auto,
                Extractor = "rule",
                ExtractorVersion = "1.0",
                SourceRefs = SourceRefBuilder.ExpandPageRects(block, raw)
            });
        }

        return Task.FromResult<IReadOnlyList<BaselineFieldDraft>>(drafts);
    }

    private static IEnumerable<JsonElement> EnumerateBlocks(JsonElement root)
    {
        if (!root.TryGetProperty("blocks", out var blocks) || blocks.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var block in blocks.EnumerateArray())
        {
            yield return block;
        }
    }

    private static string? GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

}
