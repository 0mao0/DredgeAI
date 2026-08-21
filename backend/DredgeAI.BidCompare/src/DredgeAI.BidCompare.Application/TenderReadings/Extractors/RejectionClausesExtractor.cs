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
        "{\"fieldKey\":\"snake_case_key\",\"value\":{\"text\":\"条款原文\",\"mandatory\":true,\"category\":\"资质/报价/技术/工期/格式等\"},\"rawText\":\"条款原文摘要\"}。" +
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
                var fieldKey = GetString(element, "fieldKey") ?? $"rejection_clause_{index}";
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
