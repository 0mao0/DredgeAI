using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.TenderReadings.Extractors;

/// <summary>P1 商务关键数据抽取：规则为主（限价、工期、质保期、付款方式）。</summary>
public class CommercialDataExtractor : IBaselineFieldExtractor, ITransientDependency
{
    private static readonly Regex PricePattern = new(
        @"(?:最高限价|招标控制价|控制价|预算金额|采购预算)\s*[:：]?\s*(?:人民币)?\s*([0-9][0-9,]*(?:\.[0-9]+)?)\s*(万元|元|万)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PeriodPattern = new(
        @"(?:工期|交货期|服务期|完成期限)\s*[:：]?\s*([0-9]+\s*(?:日历天|天|个?月|年))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex WarrantyPattern = new(
        @"(?:质保期|保修期|质量保证期)\s*[:：]?\s*([0-9]+\s*(?:年|个月|月))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PaymentPattern = new(
        @"(?:付款方式|支付方式)\s*[:：]?\s*([^。；\n]{2,120})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public BaselineCategory Category => BaselineCategory.CommercialData;

    public Task<IReadOnlyList<BaselineFieldDraft>> ExtractAsync(
        BaselineExtractionContext context,
        CancellationToken cancellationToken = default)
    {
        var drafts = new List<BaselineFieldDraft>();
        var candidates = new Dictionary<string, (Regex Pattern, string FieldKey, double Confidence)>
        {
            ["price"] = (PricePattern, "price_ceiling", 0.92),
            ["period"] = (PeriodPattern, "construction_period", 0.92),
            ["warranty"] = (WarrantyPattern, "warranty_period", 0.92),
            ["payment"] = (PaymentPattern, "payment_method", 0.85)
        };

        var seenKeys = new HashSet<string>();
        foreach (var block in EnumerateBlocks(context.IrRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = GetString(block, "text");
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            foreach (var candidate in candidates.Values)
            {
                if (seenKeys.Contains(candidate.FieldKey))
                {
                    continue;
                }

                var match = candidate.Pattern.Match(text);
                if (!match.Success)
                {
                    continue;
                }

                drafts.Add(new BaselineFieldDraft
                {
                    FieldKey = candidate.FieldKey,
                    ValueJson = JsonSerializer.Serialize(new { text = match.Value.Trim() }),
                    RawText = match.Value.Trim(),
                    Confidence = candidate.Confidence,
                    Status = BaselineFieldStatus.Auto,
                    Extractor = "rule",
                    ExtractorVersion = "1.0",
                    SourceRefs = new List<SourceMapItemDraft>
                    {
                        new()
                        {
                            BlockId = GetString(block, "blockId") ?? string.Empty,
                            PageIdx = GetInt(block, "pageIdx"),
                            Bbox = GetBbox(block),
                            Text = match.Value.Trim()
                        }
                    }
                });
                seenKeys.Add(candidate.FieldKey);
            }
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
