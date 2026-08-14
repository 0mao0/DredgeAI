using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.AnGineer;

/// <summary>
/// 可编程 Fake：默认立即成功并返回 SampleIr 产物包；
/// 设置 StateSequence 可模拟轮询过程，设置 FailWith 可模拟解析失败。
/// </summary>
public class FakeAnGineerClient : IAnGineerClient
{
    private readonly object _sync = new();
    private readonly Dictionary<string, string> _jobFileNames = new();
    private int _activeSubmits;

    public Queue<AnGineerJobState>? StateSequence { get; set; }

    public string? FailWith { get; set; }

    public HashSet<string> FailFileNames { get; } = new();

    public int TransientStateFailuresRemaining { get; set; }

    /// <summary>提交模拟耗时，用于验证批量解析是否并发提交。</summary>
    public int SubmitDelayMs { get; set; }

    public int MaxConcurrentSubmits { get; private set; }

    public AnGineerPackage Package { get; set; } = new(
        GraphJsonl: System.Text.Encoding.UTF8.GetBytes(SampleIr.ValidGraphJsonl),
        MetaJson: System.Text.Encoding.UTF8.GetBytes(SampleIr.ValidMetaJson),
        ContentMd: System.Text.Encoding.UTF8.GetBytes(SampleIr.ValidContentMd),
        Images: new Dictionary<string, byte[]> { ["images/t1.jpg"] = new byte[] { 0xFF, 0xD8 } });

    public async Task<string> SubmitAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        var jobId = $"fake-job-{Guid.NewGuid():N}";
        var active = System.Threading.Interlocked.Increment(ref _activeSubmits);
        lock (_sync)
        {
            _jobFileNames[jobId] = fileName;
            MaxConcurrentSubmits = Math.Max(MaxConcurrentSubmits, active);
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
            System.Threading.Interlocked.Decrement(ref _activeSubmits);
        }
    }

    public Task<AnGineerJobState> GetStateAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (FailFileNames.Count > 0)
        {
            lock (_sync)
            {
                if (_jobFileNames.TryGetValue(jobId, out var fileName) && FailFileNames.Contains(fileName))
                {
                    return Task.FromResult(AnGineerJobState.Failed);
                }
            }
        }
        if (TransientStateFailuresRemaining > 0)
        {
            TransientStateFailuresRemaining--;
            return Task.FromException<AnGineerJobState>(new HttpRequestException(
                "An error occurred while sending the request.",
                new IOException("Unable to read data from the transport connection",
                    new SocketException(10053))));
        }
        if (FailWith != null)
        {
            return Task.FromResult(AnGineerJobState.Failed);
        }
        if (StateSequence is { Count: > 0 })
        {
            return Task.FromResult(StateSequence.Dequeue());
        }
        return Task.FromResult(AnGineerJobState.Succeeded);
    }

    public Task<AnGineerPackage> DownloadPackageAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Package);
    }
}
