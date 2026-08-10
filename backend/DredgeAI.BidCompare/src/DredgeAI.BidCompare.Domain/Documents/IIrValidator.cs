namespace DredgeAI.BidCompare.Documents;

/// <summary>内部适配 IR 规范化校验（v2 文档 §2/§4/§5；spec §10 测试策略：不合格即拒收并报具体原因）。</summary>
public interface IIrValidator
{
    IrValidationResult Validate(string irJson);
}
