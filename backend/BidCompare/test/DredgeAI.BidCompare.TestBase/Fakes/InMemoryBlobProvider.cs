using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DredgeAI.BlobStoring;
using Volo.Abp.BlobStoring;

namespace DredgeAI.BidCompare.Storage;

/// <summary>IDredgeBlobProvider 内存实现，供全部测试工程使用；不实现 ABP 依赖标记，由测试模块显式注册单例。</summary>
public class InMemoryBlobProvider : IDredgeBlobProvider
{
    public ConcurrentDictionary<string, byte[]> Objects { get; } = new();

    public Task SaveAsync(BlobProviderSaveArgs args) => SaveCore(args);

    public Task SaveAsync(BlobProviderSaveArgs args, string contentType) => SaveCore(args); // contentType 忽略

    public Task<bool> DeleteAsync(BlobProviderDeleteArgs args) => Task.FromResult(Objects.TryRemove(args.BlobName, out _));

    public Task<bool> ExistsAsync(BlobProviderExistsArgs args) => Task.FromResult(Objects.ContainsKey(args.BlobName));

    public Task<Stream?> GetOrNullAsync(BlobProviderGetArgs args)
        => Task.FromResult<Stream?>(Objects.TryGetValue(args.BlobName, out var bytes) ? new MemoryStream(bytes, writable: false) : null);

    public Task<BlobFileInfo?> GetStatOrNullAsync(BlobProviderGetArgs args)
        => Task.FromResult(Objects.TryGetValue(args.BlobName, out var bytes)
            ? new BlobFileInfo { ObjectName = args.BlobName, Size = bytes.Length, ContentType = "application/octet-stream" }
            : null);

    public Task<Stream?> GetRangeOrNullAsync(BlobProviderGetArgs args, long offset, long length)
    {
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
        if (!Objects.TryGetValue(args.BlobName, out var bytes)) return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(new MemoryStream(bytes.Skip((int)offset).Take((int)length).ToArray(), writable: false));
    }

    public Task<int> DeleteByPrefixAsync(BlobProviderDeleteArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.BlobName))
        {
            throw new ArgumentException("前缀不能为空（防止空前缀清空整个容器）。", nameof(args));
        }
        var keys = Objects.Keys.Where(k => k.StartsWith(args.BlobName, StringComparison.Ordinal)).ToList();
        foreach (var key in keys) { Objects.TryRemove(key, out _); }
        return Task.FromResult(keys.Count);
    }

    public Task<string?> GetDownloadUrlAsync(BlobProviderGetArgs args, int? expirySeconds = null)
        => Task.FromResult<string?>($"memory://{args.BlobName}?expiry={expirySeconds ?? 3600}s");


    private Task SaveCore(BlobProviderSaveArgs args)
    {
        using var buffer = new MemoryStream();
        args.BlobStream.CopyTo(buffer);
        Objects[args.BlobName] = buffer.ToArray();
        return Task.CompletedTask;
    }
}
