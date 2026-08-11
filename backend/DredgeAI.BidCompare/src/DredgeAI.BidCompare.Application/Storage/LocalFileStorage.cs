using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace DredgeAI.BidCompare.Storage;

/// <summary>
/// 本地磁盘实现（开发/单机部署免去 MinIO/S3）：文件落在 RootPath 下，key 即相对路径。
/// 不实现 ABP 依赖标记，由宿主按 Storage:Provider 显式注册。
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly LocalStorageOptions _options;

    public LocalFileStorage(IOptions<LocalStorageOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>存储根目录（绝对路径）。</summary>
    public string RootPath => Path.GetFullPath(_options.RootPath);

    public async Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, 81920, cancellationToken);
        return key;
    }

    public async Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Object not found: {key}", key);
        }
        var buffer = new MemoryStream();
        await using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
        {
            await file.CopyToAsync(buffer, 81920, cancellationToken);
        }
        buffer.Position = 0;
        return buffer;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(File.Exists(ResolvePath(key)));

    public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var relative = string.Join("/", key.Replace('\\', '/').TrimStart('/').Split('/').Select(Uri.EscapeDataString));
        return Task.FromResult($"{_options.PublicBaseUrl.TrimEnd('/')}/storage/{relative}");
    }

    /// <summary>key → 绝对路径，并阻止路径穿越逃出根目录。</summary>
    private string ResolvePath(string key)
    {
        var root = Path.GetFullPath(RootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(root, key.Replace('\\', '/').TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Storage key escapes root: {key}");
        }
        return full;
    }
}
