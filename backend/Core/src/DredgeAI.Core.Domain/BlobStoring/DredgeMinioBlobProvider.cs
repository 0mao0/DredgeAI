using Minio.DataModel.Args;
using Minio.Exceptions;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.Minio;

namespace DredgeAI.BlobStoring;

/// <summary>
/// Minio blob provider：继承 <see cref="MinioBlobProvider"/>，对象名经
/// <see cref="IMinioBlobNameCalculator"/> 计算（自动带 host/tenants 租户前缀），
/// 客户端与桶名复用基类的 GetMinioClient / GetContainerName。
/// </summary>
public class DredgeMinioBlobProvider : MinioBlobProvider, IDredgeBlobProvider
{
    public DredgeMinioBlobProvider(
        IHttpClientFactory httpClientFactory,
        IMinioBlobNameCalculator minioBlobNameCalculator,
        IBlobNormalizeNamingService blobNormalizeNamingService)
        : base(httpClientFactory, minioBlobNameCalculator, blobNormalizeNamingService)
    {
    }

    /// <inheritdoc />
    public async Task SaveAsync(BlobProviderSaveArgs args, string contentType)
    {
        var blobName = MinioBlobNameCalculator.Calculate(args);
        var configuration = args.Configuration.GetMinioConfiguration();
        var client = GetMinioClient(args);
        var containerName = GetContainerName(args);

        if (!args.OverrideExisting && await BlobExistsAsync(client, containerName, blobName))
        {
            throw new BlobAlreadyExistsException($"Saving BLOB '{args.BlobName}' does already exists in the container '{containerName}'! Set {nameof(args.OverrideExisting)} if it should be overwritten.");
        }

        if (configuration.CreateBucketIfNotExists)
        {
            await CreateBucketIfNotExists(client, containerName);
        }

        // 非可寻流（如 IFormFile 直通上传流）无法提前获知长度：传 -1 让 SDK 走分片上传（按 16MiB 分片读至 EOF）。
        var objectSize = args.BlobStream.CanSeek ? args.BlobStream.Length : -1L;

        await client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(containerName)
            .WithObject(blobName)
            .WithStreamData(args.BlobStream)
            .WithObjectSize(objectSize)
            .WithContentType(contentType));
    }

    /// <inheritdoc />
    public async Task<BlobFileInfo?> GetStatOrNullAsync(BlobProviderGetArgs args)
    {
        var client = GetMinioClient(args);
        var containerName = GetContainerName(args);
        var blobName = MinioBlobNameCalculator.Calculate(args);

        try
        {
            var stat = await client.StatObjectAsync(
                new StatObjectArgs()
                    .WithBucket(containerName)
                    .WithObject(blobName),
                args.CancellationToken);

            return new BlobFileInfo
            {
                ObjectName = args.BlobName,
                Size = stat.Size,
                LastModified = stat.LastModified,
                ETag = stat.ETag,
                ContentType = stat.ContentType,
                MetaData = stat.MetaData is null
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>(stat.MetaData)
            };
        }
        catch (ObjectNotFoundException)
        {
            return null;
        }
        catch (BucketNotFoundException)
        {
            return null;
        }
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

        var client = GetMinioClient(args);
        var containerName = GetContainerName(args);
        var blobName = MinioBlobNameCalculator.Calculate(args);

        // 先探测，避免 GetObject 在对象缺失时抛异常。
        if (!await BlobExistsAsync(client, containerName, blobName))
        {
            return null;
        }

        var memoryStream = new MemoryStream();
        await client.GetObjectAsync(
            new GetObjectArgs()
                .WithBucket(containerName)
                .WithObject(blobName)
                .WithOffsetAndLength(offset, length)
                .WithCallbackStream(s => s.CopyTo(memoryStream)),
            args.CancellationToken);

        memoryStream.Position = 0;
        return memoryStream;
    }

    /// <inheritdoc />
    public async Task<int> DeleteByPrefixAsync(BlobProviderDeleteArgs args)
    {
        var prefix = MinioBlobNameCalculator.Calculate(args);
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("前缀不能为空（防止空前缀清空整个容器）。", nameof(args));
        }

        var client = GetMinioClient(args);
        var containerName = GetContainerName(args);

        var keys = new List<string>();
        await foreach (var item in client.ListObjectsEnumAsync(
                           new ListObjectsArgs()
                               .WithBucket(containerName)
                               .WithPrefix(prefix)
                               .WithRecursive(true),
                           args.CancellationToken))
        {
            if (!item.IsDir)
            {
                keys.Add(item.Key);
            }
        }

        if (keys.Count == 0)
        {
            return 0;
        }

        var deleteErrors = await client.RemoveObjectsAsync(
            new RemoveObjectsArgs()
                .WithBucket(containerName)
                .WithObjects(keys),
            args.CancellationToken);

        if (deleteErrors.Count > 0)
        {
            throw new AbpException(
                $"删除 blob 失败，共 {deleteErrors.Count} 个对象未删除：{string.Join(", ", deleteErrors.Select(e => e.Key))}");
        }

        return keys.Count;
    }

    /// <inheritdoc />
    public async Task<string?> GetDownloadUrlAsync(BlobProviderGetArgs args, int? expirySeconds = null)
    {
        var client = GetMinioClient(args);
        var containerName = GetContainerName(args);
        var blobName = MinioBlobNameCalculator.Calculate(args);

        if (!await BlobExistsAsync(client, containerName, blobName))
        {
            return null;
        }

        var configuration = args.Configuration.GetMinioConfiguration();
        return await client.PresignedGetObjectAsync(
            new PresignedGetObjectArgs()
                .WithBucket(containerName)
                .WithObject(blobName)
                .WithExpiry(expirySeconds ?? configuration.PresignedGetExpirySeconds));
    }
}
