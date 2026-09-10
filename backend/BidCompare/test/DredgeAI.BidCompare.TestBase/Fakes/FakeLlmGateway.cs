using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.AI;

/// <summary>队列式 Fake：按调用顺序返回 QueueResponse 预置的响应；耗尽即抛异常暴露未预期调用。</summary>
public class FakeLlmGateway : ILlmGateway
{
    private readonly Queue<string> _responses = new();

    public List<(string System, string User)> Requests { get; } = new();

    public List<(string System, string Text, int ImageCount)> MultimodalRequests { get; } = new();

    /// <summary>流式请求记录（与 Requests 同源，便于断言流式调用）。</summary>
    public List<(string System, string User)> StreamRequests { get; } = new();

    public bool ThrowOnNextCall { get; set; }

    public void QueueResponse(string response) => _responses.Enqueue(response);

    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        Requests.Add((systemPrompt, userPrompt));
        if (ThrowOnNextCall)
        {
            ThrowOnNextCall = false;
            throw new System.InvalidOperationException("LLM service unavailable");
        }
        if (_responses.Count == 0)
        {
            throw new System.InvalidOperationException("FakeLlmGateway：响应队列已空，存在未预期的 LLM 调用");
        }
        return Task.FromResult(_responses.Dequeue());
    }

    /// <summary>流式 Fake：取出队列响应后按字符逐段产出，模拟真实增量推送。</summary>
    public async IAsyncEnumerable<string> CompleteStreamAsync(
        string systemPrompt,
        string userPrompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        StreamRequests.Add((systemPrompt, userPrompt));
        if (ThrowOnNextCall)
        {
            ThrowOnNextCall = false;
            throw new System.InvalidOperationException("LLM service unavailable");
        }
        if (_responses.Count == 0)
        {
            throw new System.InvalidOperationException("FakeLlmGateway：响应队列已空，存在未预期的 LLM 调用");
        }
        var response = _responses.Dequeue();
        foreach (var ch in response)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return ch.ToString();
            await Task.Yield();
        }
    }

    public Task<string> CompleteMultimodalAsync(
        string systemPrompt,
        string text,
        IReadOnlyList<LlmImageInput> images,
        CancellationToken cancellationToken = default)
    {
        MultimodalRequests.Add((systemPrompt, text, images.Count));
        if (ThrowOnNextCall)
        {
            ThrowOnNextCall = false;
            throw new System.InvalidOperationException("LLM service unavailable");
        }
        if (_responses.Count == 0)
        {
            throw new System.InvalidOperationException("FakeLlmGateway：响应队列已空，存在未预期的 LLM 调用");
        }
        return Task.FromResult(_responses.Dequeue());
    }
}
