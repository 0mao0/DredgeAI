using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.TenderReadings.Extractors;

/// <summary>P3 签章规则抽取：关键词匹配盖章、签字、骑缝等要求。</summary>
public class SealRulesExtractor : IBaselineFieldExtractor, ITransientDependency
{
    private static readonly string[] Keywords = { "盖章", "签字", "骑缝", "公章", "法定代表人签字" };

    public BaselineCategory Category => BaselineCategory.SealRules;

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
                FieldKey = $"seal_rule_{index}",
                ValueJson = JsonSerializer.Serialize(new { value = raw }),
                RawText = raw,
                Confidence = 0.9,
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
