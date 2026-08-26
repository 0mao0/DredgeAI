using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.TenderReadings.Extractors;

/// <summary>P2 评分标准 LLM 抽取：维度、子项、分值、扣分规则。</summary>
public class EvaluationCriteriaExtractor : LlmFieldExtractorBase, IBaselineFieldExtractor, ITransientDependency
{
    private const string SystemPrompt =
        "你是招投标文件分析助手。从招标文件全文中提取评分标准，包括评分维度、子项、分值与扣分规则。" +
        "只返回 JSON 数组，不要输出任何其他文字。";

    private const string UserPromptTemplate =
        "以下是招标文件全文：\n\n{{DOCUMENT}}\n\n" +
        "请以 JSON 数组返回评分标准，每项格式：" +
        "{\"fieldKey\":\"标准英文术语\",\"value\":{\"dimension\":\"评分维度\",\"score\":10,\"subItems\":[\"子项1\",\"子项2\"],\"deductionRules\":\"扣分规则\"},\"rawText\":\"命中原文的逐字引用\"}。" +
        "rawText 必须是招标文件原文的逐字引用（不得转述或概括），可直接在原文中检索到；跨多行/多单元格时取其中最完整的一段，不超过 120 字。" +
        "fieldKey 只能使用常见招投标标准术语（如 price_score、technical_solution、schedule_plan 等），不得发明自定义词组；" +
        "若无法用标准术语概括，使用 evaluation_criteria_序号（序号从 1 开始连续编号）。" +
        "只返回 JSON。";

    public EvaluationCriteriaExtractor(ILlmGateway llmGateway) : base(llmGateway)
    {
    }

    public BaselineCategory Category => BaselineCategory.EvaluationCriteria;

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
                var rawText = GetString(element, "rawText") ?? string.Empty;
                var value = element.TryGetProperty("value", out var v) ? v : element;
                return new BaselineFieldDraft
                {
                    FieldKey = fieldKey,
                    ValueJson = value.ValueKind == JsonValueKind.Object ? value.GetRawText() : JsonSerializer.Serialize(new { dimension = rawText }),
                    RawText = rawText,
                    Confidence = 0.82,
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
}
