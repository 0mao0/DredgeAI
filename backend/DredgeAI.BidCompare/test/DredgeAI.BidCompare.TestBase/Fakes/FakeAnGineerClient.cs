using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.AnGineer;

/// <summary>
/// 可编程 Fake：默认立即成功并返回 SampleIr 产物包；
/// 设置 StateSequence 可模拟轮询过程，设置 FailWith 可模拟解析失败。
/// </summary>
public class FakeAnGineerClient : IAnGineerClient
{
    public Queue<AnGineerJobState>? StateSequence { get; set; }

    public string? FailWith { get; set; }

    public AnGineerPackage Package { get; set; } = new(
        GraphJsonl: System.Text.Encoding.UTF8.GetBytes(SampleIr.ValidGraphJsonl),
        MetaJson: System.Text.Encoding.UTF8.GetBytes(SampleIr.ValidMetaJson),
        ContentMd: System.Text.Encoding.UTF8.GetBytes(SampleIr.ValidContentMd),
        Images: new Dictionary<string, byte[]> { ["images/t1.jpg"] = new byte[] { 0xFF, 0xD8 } });

    public Task<string> SubmitAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        return Task.FromResult("fake-angineer-job-1");
    }

    public Task<AnGineerJobState> GetStateAsync(string jobId, CancellationToken cancellationToken = default)
    {
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
