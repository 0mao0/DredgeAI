using System.Collections.Generic;
using System.Text.Json;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.TenderReadings.Extractors;

/// <summary>P1 极简 Schema 校验：ValueJson 必须是合法 JSON；目录树必须是数组，其余类别为对象。</summary>
public class BaselineSchemaValidator : IBaselineSchemaValidator, ITransientDependency
{
    public IReadOnlyList<string> Validate(BaselineCategory category, string fieldKey, string valueJson)
    {
        var errors = new List<string>();
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(valueJson);
        }
        catch (JsonException ex)
        {
            errors.Add($"{category}/{fieldKey} ValueJson 不是合法 JSON：{ex.Message}");
            return errors;
        }

        using (document)
        {
            if (category == BaselineCategory.ChapterOutline)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    errors.Add($"{category}/{fieldKey} 目录树 ValueJson 必须是数组");
                }
            }
            else if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{category}/{fieldKey} ValueJson 必须是对象");
            }
        }

        return errors;
    }
}
