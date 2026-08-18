using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.AnGineer;

/// <summary>
/// 可编程 Fake：默认立即成功并返回 SampleIr 产物包；
/// 设置 StateSequence 可模拟轮询过程，设置 FailWith 可模拟解析失败。
/// 并发安全：状态序列/瞬时失败计数均可被批量解析并发访问。
/// </summary>
public class FakeAnGineerClient : IAnGineerClient
{
    private readonly object _sync = new();
    private readonly Dictionary<string, string> _jobFileNames = new();
    private int _activeSubmits;
    private int _transientStateFailuresRemaining;

    public ConcurrentQueue<AnGineerJobStatus>? StateSequence { get; set; }

    /// <summary>轮询始终返回的固定状态（模拟 AnGIneer 任务停滞）；StateSequence 耗尽后生效。</summary>
    public AnGineerJobStatus? RepeatingState { get; set; }

    /// <summary>resume 返回的状态序列；缺省时返回 Processing。</summary>
    public ConcurrentQueue<AnGineerJobStatus>? ResumeSequence { get; set; }

    public string? FailWith { get; set; }

    /// <summary>resume 失败原因（用于模拟恢复仍失败）。</summary>
    public string? ResumeFailWith { get; set; }

    public HashSet<string> FailFileNames { get; } = new();

    /// <summary>产物清单中不返回的名称（如 doc_blocks_graph.jsonl），用于模拟 partial 缺产物。</summary>
    public HashSet<string> MissingArtifacts { get; } = new();

    /// <summary>剩余瞬时轮询失败次数；配合 TransientStatusCode 可模拟 5xx（缺省模拟连接重置 IOException）。</summary>
    public int TransientStateFailuresRemaining
    {
        get => _transientStateFailuresRemaining;
        set => _transientStateFailuresRemaining = value;
    }

    /// <summary>瞬时失败使用的 HTTP 状态码（如 503）；null = 连接重置（内层 SocketException）。</summary>
    public HttpStatusCode? TransientStatusCode { get; set; }

    /// <summary>提交模拟耗时，用于验证批量解析是否并发提交。</summary>
    public int SubmitDelayMs { get; set; }

    public int MaxConcurrentSubmits { get; private set; }

    /// <summary>累计提交次数（幂等性断言用）。</summary>
    public int SubmitCount { get; private set; }

    private int _resumeCount;

    public int ResumeCount => _resumeCount;

    public AnGineerPackage Package { get; set; } = new(
        GraphJsonl: System.Text.Encoding.UTF8.GetBytes(SampleIr.ValidGraphJsonl),
        MetaJson: System.Text.Encoding.UTF8.GetBytes(SampleIr.ValidMetaJson),
        ContentMd: System.Text.Encoding.UTF8.GetBytes(SampleIr.ValidContentMd),
        Images: new Dictionary<string, byte[]> { ["images/t1.jpg"] = new byte[] { 0xFF, 0xD8 } });

    public async Task<string> SubmitAsync(string fileName, Func<Task<Stream>> openContent, CancellationToken cancellationToken = default)
    {
        await using var _ = await openContent();
        var jobId = $"fake-job-{Guid.NewGuid():N}";
        var active = Interlocked.Increment(ref _activeSubmits);
        lock (_sync)
        {
            _jobFileNames[jobId] = fileName;
            MaxConcurrentSubmits = Math.Max(MaxConcurrentSubmits, active);
            SubmitCount++;
        }
        try
        {
            if (SubmitDelayMs > 0)
            {
                await Task.Delay(SubmitDelayMs, cancellationToken);
            }
            return jobId;
        }
        finally
        {
            Interlocked.Decrement(ref _activeSubmits);
        }
    }

    public Task<AnGineerJobStatus> GetStateAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (FailFileNames.Count > 0)
        {
            lock (_sync)
            {
                if (_jobFileNames.TryGetValue(jobId, out var fileName) && FailFileNames.Contains(fileName))
                {
                    return Task.FromResult(new AnGineerJobStatus(
                        AnGineerJobState.Failed, 100, "failed", "解析失败"));
                }
            }
        }
        if (Interlocked.CompareExchange(ref _transientStateFailuresRemaining, 0, 0) > 0)
        {
            if (Interlocked.Decrement(ref _transientStateFailuresRemaining) >= 0)
            {
                return Task.FromException<AnGineerJobStatus>(BuildTransientException());
            }
            Interlocked.Increment(ref _transientStateFailuresRemaining); // 并发下扣到负数则回补
        }
        if (FailWith != null)
        {
            return Task.FromResult(new AnGineerJobStatus(
                AnGineerJobState.Failed, 100, "failed", FailWith));
        }
        if (StateSequence is { IsEmpty: false } sequence)
        {
            if (sequence.TryDequeue(out var status))
            {
                return Task.FromResult(status);
            }
        }
        if (RepeatingState != null)
        {
            return Task.FromResult(RepeatingState);
        }
        return Task.FromResult(new AnGineerJobStatus(
            AnGineerJobState.Succeeded, 100, "completed", "解析结束: completed"));
    }

    public Task<AnGineerJobStatus> ResumeAsync(string jobId, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _resumeCount);
        if (FailFileNames.Count > 0)
        {
            lock (_sync)
            {
                if (_jobFileNames.TryGetValue(jobId, out var fileName) && FailFileNames.Contains(fileName))
                {
                    return Task.FromResult(new AnGineerJobStatus(
                        AnGineerJobState.Failed, 100, "failed", "解析失败"));
                }
            }
        }
        if (ResumeFailWith != null)
        {
            return Task.FromResult(new AnGineerJobStatus(
                AnGineerJobState.Failed, 100, "failed", ResumeFailWith));
        }
        var resumeSequence = ResumeSequence;
        if (resumeSequence is { IsEmpty: false } && resumeSequence.TryDequeue(out var status))
        {
            return Task.FromResult(status);
        }
        return Task.FromResult(new AnGineerJobStatus(
            AnGineerJobState.Processing, 0, "processing", "恢复解析中"));
    }

    public Task<IReadOnlyList<AnGineerArtifact>> ListArtifactsAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var items = new List<AnGineerArtifact>
        {
            new("doc_blocks_graph.jsonl", $"fake://{jobId}/doc_blocks_graph.jsonl"),
            new("doc_blocks_graph_meta.json", $"fake://{jobId}/doc_blocks_graph_meta.json")
        };
        if (Package.ContentMd != null)
        {
            items.Add(new AnGineerArtifact("content.md", $"fake://{jobId}/content.md"));
        }
        items.AddRange(Package.Images.Keys.Select(name => new AnGineerArtifact(name, $"fake://{jobId}/{name}")));
        items.RemoveAll(item => MissingArtifacts.Contains(item.Name));
        return Task.FromResult<IReadOnlyList<AnGineerArtifact>>(items);
    }

    public Task<Stream> OpenArtifactAsync(string jobId, AnGineerArtifact artifact, CancellationToken cancellationToken = default)
    {
        byte[] bytes = artifact.Name switch
        {
            "doc_blocks_graph.jsonl" => Package.GraphJsonl,
            "doc_blocks_graph_meta.json" => Package.MetaJson,
            "content.md" => Package.ContentMd ?? throw new FileNotFoundException(artifact.Name),
            _ when Package.Images.TryGetValue(artifact.Name, out var image) => image,
            _ => throw new FileNotFoundException(artifact.Name)
        };
        return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
    }

    private HttpRequestException BuildTransientException()
        => TransientStatusCode.HasValue
            ? new HttpRequestException($"AnGIneer 响应 {(int)TransientStatusCode.Value}", null, TransientStatusCode.Value)
            : new HttpRequestException(
                "An error occurred while sending the request.",
                new IOException("Unable to read data from the transport connection",
                    new SocketException(10053)));
}
