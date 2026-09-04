using Microsoft.Extensions.Options;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.FileSystem;

namespace DredgeAI.BlobStoring;

/// <summary>
/// FileSystem blob provider：继承 <see cref="FileSystemBlobProvider"/>，路径一律经
/// <see cref="IBlobFilePathCalculator"/> 计算（自动带 host/tenants 租户子目录）。
/// </summary>
public class DredgeFileSystemBlobProvider : FileSystemBlobProvider, IDredgeBlobProvider
{
    private readonly IOptions<BlobFileSystemSigningOptions> _signingOptions;

    public DredgeFileSystemBlobProvider(
        IBlobFilePathCalculator filePathCalculator,
        IOptions<BlobFileSystemSigningOptions> signingOptions)
        : base(filePathCalculator)
    {
        _signingOptions = signingOptions;
    }

    /// <inheritdoc />
    public virtual Task SaveAsync(BlobProviderSaveArgs args, string contentType) => base.SaveAsync(args);

    /// <inheritdoc />
    public Task<BlobFileInfo?> GetStatOrNullAsync(BlobProviderGetArgs args)
    {
        var path = FilePathCalculator.Calculate(args);
        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            return Task.FromResult<BlobFileInfo?>(null);
        }

        return Task.FromResult<BlobFileInfo?>(new BlobFileInfo
        {
            ObjectName = args.BlobName,
            Size = fileInfo.Length,
            LastModified = fileInfo.LastWriteTimeUtc,
            ETag = null,
            ContentType = "application/octet-stream"
        });
    }

    /// <inheritdoc />
    public async Task<Stream?> GetRangeOrNullAsync(BlobProviderGetArgs args, long offset, long length)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "offset 不能为负数。");
        }

        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "length 必须大于 0。");
        }

        var path = FilePathCalculator.Calculate(args);
        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            return null;
        }

        var memoryStream = new MemoryStream();
        if (offset < fileInfo.Length)
        {
            await using var fileStream = File.OpenRead(path);
            fileStream.Seek(offset, SeekOrigin.Begin);
            var remaining = (int)Math.Min(length, fileInfo.Length - offset);
            var buffer = new byte[remaining];
            var read = 0;
            while (read < remaining)
            {
                var n = await fileStream.ReadAsync(
                    buffer.AsMemory(read, remaining - read),
                    args.CancellationToken);
                if (n == 0)
                {
                    break;
                }

                read += n;
            }

            memoryStream.Write(buffer, 0, read);
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    /// <inheritdoc />
    public Task<int> DeleteByPrefixAsync(BlobProviderDeleteArgs args)
    {
        var prefix = args.BlobName;
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("前缀不能为空（防止空前缀清空整个容器）。", nameof(args));
        }

        // 探针法取得容器目录：探针 blob 名无子目录段，其目录即容器目录，
        // 租户/AppendContainerNameToBasePath 逻辑全部交给 calculator。
        var probePath = FilePathCalculator.Calculate(
            new BlobProviderDeleteArgs(args.ContainerName, args.Configuration, "__dredge_probe__", args.CancellationToken));
        var dir = Path.GetDirectoryName(probePath);

        if (dir is null || !Directory.Exists(dir))
        {
            return Task.FromResult(0);
        }

        var count = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(dir, file).Replace('\\', '/');
            if (!relativePath.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            File.Delete(file);
            count++;
        }

        return Task.FromResult(count);
    }

    /// <inheritdoc />
    public Task<string?> GetDownloadUrlAsync(BlobProviderGetArgs args, int? expirySeconds = null)
    {
        var path = FilePathCalculator.Calculate(args);
        if (!File.Exists(path))
        {
            return Task.FromResult<string?>(null);
        }
        var options = _signingOptions.Value;
        var expires = DateTimeOffset.UtcNow
            .AddSeconds(expirySeconds ?? options.DefaultExpirySeconds)
            .ToUnixTimeSeconds();
        return Task.FromResult<string?>(
            BlobDownloadUrlSigner.BuildUrl(options.DownloadEndpointPath, options.SigningSecret!, args.BlobName, expires));
    }
}
