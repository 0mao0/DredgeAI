using System;
using System.Collections.Generic;
using System.Text.Json;
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
        "fieldKey 只能使用常见招投标标准术语（如 bid_security、qualification、payment_terms 等），不得发明自定义词组；" +
        "若条款无法用标准术语概括，使用 rejection_clause_序号（序号从 1 开始连续编号）。" +
        "只返回 JSON。";

    public RejectionClausesExtractor(ILlmGateway llmGateway) : base(llmGateway)
    {
    }

    public BaselineCategory Category => BaselineCategory.RejectionClauses;

    public async Task<IReadOnlyList<BaselineFieldDraft>> ExtractAsync(
        BaselineExtractionContext context,
        CancellationToken cancellationToken = default)
    {
        var index = 0;
        return await ExtractByLlmAsync(
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
