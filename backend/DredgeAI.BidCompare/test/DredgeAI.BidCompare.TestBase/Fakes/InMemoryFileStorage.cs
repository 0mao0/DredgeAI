using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.Storage;

/// <summary>IFileStorage 内存实现，供全部测试工程使用。</summary>
public class InMemoryFileStorage : IFileStorage
{
    public ConcurrentDictionary<string, byte[]> Objects { get; } = new();

    public Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        Objects[key] = buffer.ToArray();
        return Task.FromResult(key);
    }

    public Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!Objects.TryGetValue(key, out var bytes))
        {
            throw new FileNotFoundException($"Object not found: {key}", key);
        }
        return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        Objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Objects.ContainsKey(key));
    }

    public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"memory://{key}?expiry={expiry.TotalMinutes}m");
    }
}
