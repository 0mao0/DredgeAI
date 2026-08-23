using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.TenderReadings.Extractors;

/// <summary>P2 技术参数规格表 LLM 抽取：参数名、要求值、单位、是否实质性。</summary>
public class TechnicalParametersExtractor : LlmFieldExtractorBase, IBaselineFieldExtractor, ITransientDependency
{
    private const string SystemPrompt =
        "你是招投标文件分析助手。从招标文件全文中提取技术参数规格要求，包括参数名、要求值、单位与是否实质性条款。" +
        "只返回 JSON 数组，不要输出任何其他文字。";

    private const string UserPromptTemplate =
        "以下是招标文件全文：\n\n{{DOCUMENT}}\n\n" +
        "请以 JSON 数组返回技术参数，每项格式：" +
        "{\"fieldKey\":\"technical_parameter_序号\",\"value\":{\"name\":\"参数名\",\"requiredValue\":\"要求值\",\"unit\":\"单位\",\"substantive\":true},\"rawText\":\"原文摘要\"}。" +
        "fieldKey 一律使用 technical_parameter_序号（序号从 1 开始连续编号），不要发明其他 key。" +
        "只返回 JSON。";

    public TechnicalParametersExtractor(ILlmGateway llmGateway) : base(llmGateway)
    {
    }

    public BaselineCategory Category => BaselineCategory.TechnicalParameters;

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
                    ValueJson = value.ValueKind == JsonValueKind.Object ? value.GetRawText() : JsonSerializer.Serialize(new { name = rawText }),
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
