using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.AI;

/// <summary>队列式 Fake：按调用顺序返回 QueueResponse 预置的响应；耗尽即抛异常暴露未预期调用。</summary>
public class FakeLlmGateway : ILlmGateway
{
    private readonly Queue<string> _responses = new();

    public List<(string System, string User)> Requests { get; } = new();

    public void QueueResponse(string response) => _responses.Enqueue(response);

    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        Requests.Add((systemPrompt, userPrompt));
        if (_responses.Count == 0)
        {
            throw new System.InvalidOperationException("FakeLlmGateway：响应队列已空，存在未预期的 LLM 调用");
        }
        return Task.FromResult(_responses.Dequeue());
    }
}
