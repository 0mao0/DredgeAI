using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp;

namespace DredgeAI.BidCompare.Storage;

/// <summary>
/// 本地磁盘实现（开发/单机部署免去 MinIO/S3）：文件落在 RootPath 下，key 即相对路径。
/// 不实现 ABP 依赖标记，由宿主按 Storage:Provider 显式注册。
/// 下载链接为 HMAC-SHA256(key + expiry) 签名 URL，由宿主的 StorageFileController 校验后流式返回，
/// 不再暴露匿名静态文件目录。
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

    /// <summary>流式读取：直接返回文件流，不整对象读入内存（200MB 级标书防 OOM）。</summary>
    public Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Object not found: {key}", key);
        }
        return Task.FromResult<Stream>(
            new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true));
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

    /// <summary>生成限时签名下载链接：HMAC-SHA256(secret, key + "\n" + expiresUnixSeconds)。</summary>
    public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var expires = DateTimeOffset.UtcNow.Add(expiry).ToUnixTimeSeconds();
        var signature = Sign(key, expires);
        return Task.FromResult(
            $"{_options.PublicBaseUrl.TrimEnd('/')}/api/compare/storage/file" +
            $"?key={Uri.EscapeDataString(key)}&expires={expires}&sig={signature}");
    }

    /// <summary>校验签名 URL（常量时间比较，过期/篡改/未配置密钥均失败）。</summary>
    public bool ValidateSignedUrl(string key, long expires, string? signature)
    {
        if (_options.SigningSecret.IsNullOrWhiteSpace() || signature.IsNullOrWhiteSpace())
        {
            return false;
        }
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expires)
        {
            return false;
        }
        var expected = ComputeSignature(key, expires, _options.SigningSecret!);
        var expectedBytes = Encoding.ASCII.GetBytes(expected);
        var actualBytes = Encoding.ASCII.GetBytes(signature!);
        return expectedBytes.Length == actualBytes.Length
               && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private string Sign(string key, long expires)
    {
        if (_options.SigningSecret.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException(
                "Storage:Local:SigningSecret 未配置，无法生成本地存储签名 URL（经 STORAGE_LOCAL_SIGNING_SECRET 环境变量注入）。");
        }
        return ComputeSignature(key, expires, _options.SigningSecret!);
    }

    private static string ComputeSignature(string key, long expires, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(key + "\n" + expires));
        return Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_').TrimEnd('=');
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
