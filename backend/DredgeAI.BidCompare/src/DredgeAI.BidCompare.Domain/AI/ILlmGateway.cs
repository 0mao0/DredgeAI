using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.AI;

/// <summary>
/// LLM 网关（OpenAI 兼容协议，可配置 endpoint/model/key）。
/// 上层（条款提取/响应判定/指标抽取）负责 prompt 与响应 JSON 解析，网关只做对话补全。
/// </summary>
public interface ILlmGateway
{
    /// <summary>单次对话补全，返回 assistant 文本内容。</summary>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}
