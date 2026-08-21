using System.Collections.Generic;

namespace DredgeAI.BidCompare.TenderReadings;

/// <summary>抽取结果 JSON Schema 校验接口。</summary>
public interface IBaselineSchemaValidator
{
    /// <summary>返回空列表表示通过；否则返回可读错误。</summary>
    IReadOnlyList<string> Validate(BaselineCategory category, string fieldKey, string valueJson);
}
