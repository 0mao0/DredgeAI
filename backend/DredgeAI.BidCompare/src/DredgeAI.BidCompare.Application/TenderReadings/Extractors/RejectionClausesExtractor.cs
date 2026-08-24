using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.TenderReadings.Extractors;

/// <summary>P2 废标 / 无效投标条款 LLM 抽取。</summary>
public class RejectionClausesExtractor : LlmFieldExtractorBase, IBaselineFieldExtractor, ITransientDependency
{
    private const string SystemPrompt =
        "你是招投标文件分析助手。从招标文件全文中提取所有废标、无效投标、否决投标相关的强制性条款。" +
        "只返回 JSON 数组，不要输出任何其他文字。";

    private const string UserPromptTemplate =
        "以下是招标文件全文：\n\n{{DOCUMENT}}\n\n" +
        "请以 JSON 数组返回废标/无效投标条款，每项格式：" +
        "{\"fieldKey\":\"标准英文术语\",\"value\":{\"text\":\"条款原文\",\"mandatory\":true,\"category\":\"资质/报价/技术/工期/格式等\"},\"rawText\":\"条款原文的逐字引用\"}。" +
        "rawText 必须是招标文件原文的逐字引用（不得转述或概括），可直接在原文中检索到；跨多行/多单元格时取其中最完整的一段，不超过 120 字。" +
        "若条款原文是 1、2、3 等编号列表，必须将每个编号项拆分为独立的条款字段（fieldKey 用 rejection_clause_序号），禁止把整段列表合并成一条；" +
        "fieldKey 只能使用常见招投标标准术语（如 bid_security、qualification、payment_terms 等），不得发明自定义词组；" +
        "若条款无法用标准术语概括，使用 rejection_clause_序号（序号从 1 开始连续编号）。" +
        "只返回 JSON。";

    /// <summary>识别「1、2、3」/「（1）（2）」/「1. 2.」编号列表项的起点；排除 4.2 这类编号小节的误匹配。</summary>
    private static readonly Regex NumberedItemPattern = new(
        @"(?:^|(?<=[；;。：:！!？?]))\s*(?:\d{1,3}\s*[、.．](?!\d)|[（(]\d{1,2}[）)])",
        RegexOptions.Compiled);

    public RejectionClausesExtractor(ILlmGateway llmGateway) : base(llmGateway)
    {
    }

    public BaselineCategory Category => BaselineCategory.RejectionClauses;

    public async Task<IReadOnlyList<BaselineFieldDraft>> ExtractAsync(
        BaselineExtractionContext context,
        CancellationToken cancellationToken = default)
    {
        var index = 0;
        var drafts = await ExtractByLlmAsync(
            context,
            SystemPrompt,
            UserPromptTemplate,
            element =>
            {
                index++;
                var fieldKey = BaselineFieldKeys.Normalize(Category, GetString(element, "fieldKey"), index);
                var valueObj = GetObject(element, "value");
                var rawText = GetString(element, "rawText")
                    ?? (valueObj.HasValue ? GetString(valueObj.Value, "text") : null)
                    ?? string.Empty;
                var value = element.TryGetProperty("value", out var v) ? v : element;
                return new BaselineFieldDraft
                {
                    FieldKey = fieldKey,
                    ValueJson = value.ValueKind == JsonValueKind.Object ? value.GetRawText() : JsonSerializer.Serialize(new { text = rawText }),
                    RawText = rawText,
                    Confidence = 0.85,
                    Status = BaselineFieldStatus.Auto,
                    Extractor = "llm",
                    ExtractorVersion = "1.0",
                    SourceRefs = new List<SourceMapItemDraft>()
                };
            },
            cancellationToken);
        return SplitMergedNumberedClauses(context, drafts);
    }

    /// <summary>
    /// LLM 偶发把「1、2、3」编号列表合并成一条：这里确定性拆回逐条，
    /// 每条独立编号（rejection_clause_N）并重新匹配各自的溯源锚点。
    /// 引导句并入第 1 条，保证原文不丢内容。
    /// </summary>
    private static IReadOnlyList<BaselineFieldDraft> SplitMergedNumberedClauses(
        BaselineExtractionContext context,
        IReadOnlyList<BaselineFieldDraft> drafts)
    {
        var result = new List<BaselineFieldDraft>();
        var seq = 0;

        foreach (var draft in drafts)
        {
            var segments = SplitNumberedSegments(draft.RawText);
            if (segments.Count > 1)
            {
                foreach (var segment in segments)
                {
                    seq++;
                    result.Add(BuildSegmentDraft(context, draft, segment, $"rejection_clause_{seq}"));
                }
                continue;
            }

            // 非列表条款：保留语义 key（词表内），否则按序号重排
            var key = BaselineFieldKeys.Allowed.Contains(draft.FieldKey) ? draft.FieldKey : $"rejection_clause_{++seq}";
            result.Add(CopyDraft(draft, key));
        }

        return result;
    }

    private static List<string> SplitNumberedSegments(string rawText)
    {
        var text = rawText?.Trim() ?? string.Empty;
        var matches = NumberedItemPattern.Matches(text);
        if (matches.Count < 2)
        {
            return new List<string> { text };
        }

        var segments = new List<string>();
        var firstEnd = matches.Count > 1 ? matches[1].Index : text.Length;
        segments.Add(text[0..firstEnd].Trim());
        for (var i = 1; i < matches.Count; i++)
        {
            var start = matches[i].Index;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
            var segment = text[start..end].Trim();
            if (segment.Length > 0)
            {
                segments.Add(segment);
            }
        }

        return segments;
    }

    private static BaselineFieldDraft BuildSegmentDraft(
        BaselineExtractionContext context,
        BaselineFieldDraft source,
        string segment,
        string fieldKey)
    {
        return new BaselineFieldDraft
        {
            FieldKey = fieldKey,
            ValueJson = RebuildValueJson(source.ValueJson, segment),
            RawText = segment,
            Confidence = source.Confidence,
            Status = source.Status,
            Extractor = source.Extractor,
            ExtractorVersion = source.ExtractorVersion,
            SourceRefs = FindSourceRefs(context.IrRoot, segment)
        };
    }

    private static BaselineFieldDraft CopyDraft(BaselineFieldDraft source, string fieldKey)
    {
        return new BaselineFieldDraft
        {
            FieldKey = fieldKey,
            ValueJson = source.ValueJson,
            RawText = source.RawText,
            Confidence = source.Confidence,
            Status = source.Status,
            Extractor = source.Extractor,
            ExtractorVersion = source.ExtractorVersion,
            SourceRefs = source.SourceRefs
        };
    }

    /// <summary>重建条款 value：保留 mandatory/category 等字段，仅替换 text 为拆分后的单条原文。</summary>
    private static string RebuildValueJson(string originalValueJson, string segment)
    {
        try
        {
            var node = JsonNode.Parse(originalValueJson)?.AsObject();
            if (node == null)
            {
                return JsonSerializer.Serialize(new { text = segment });
            }
            node["text"] = segment;
            return node.ToJsonString();
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(new { text = segment });
        }
    }

    private static string? GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static JsonElement? GetObject(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : null;
}
