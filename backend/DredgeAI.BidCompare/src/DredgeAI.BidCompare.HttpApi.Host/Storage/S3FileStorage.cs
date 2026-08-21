using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace DredgeAI.BidCompare.Storage;

/// <summary>S3 兼容对象存储实现（MinIO），统一使用 AWSSDK.S3。</summary>
public class S3FileStorage : IFileStorage
{
    private readonly S3StorageOptions _options;
    private readonly Lazy<IAmazonS3> _client;
    private int _bucketEnsured;

    public S3FileStorage(IOptions<S3StorageOptions> options)
    {
        _options = options.Value;
        _client = new Lazy<IAmazonS3>(() => new AmazonS3Client(
            new BasicAWSCredentials(_options.AccessKey, _options.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = _options.ServiceUrl,
                ForcePathStyle = _options.ForcePathStyle
            }));
    }

    public async Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        await EnsureBucketOnceAsync(cancellationToken);
        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType
        };
        await _client.Value.PutObjectAsync(request, cancellationToken);
        return key;
    }

    /// <summary>流式读取：直接返回 S3 响应流（随流释放响应句柄），不整对象读入内存。</summary>
    public async Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var response = await _client.Value.GetObjectAsync(_options.Bucket, key, cancellationToken);
        return new OwnedStream(response.ResponseStream, response);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        await _client.Value.DeleteObjectAsync(_options.Bucket, key, cancellationToken);
    }

    public async Task DeleteByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        string? continuationToken = null;
        do
        {
            var list = await _client.Value.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _options.Bucket,
                Prefix = prefix,
                ContinuationToken = continuationToken
            }, cancellationToken);
            if (list.S3Objects.Count > 0)
            {
                await _client.Value.DeleteObjectsAsync(new DeleteObjectsRequest
                {
                    BucketName = _options.Bucket,
                    Objects = list.S3Objects.Select(o => new KeyVersion { Key = o.Key }).ToList()
                }, cancellationToken);
            }
            continuationToken = list.IsTruncated == true ? list.NextContinuationToken : null;
        } while (continuationToken != null);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.Value.GetObjectMetadataAsync(_options.Bucket, key, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Expires = DateTime.UtcNow.Add(expiry),
            Verb = HttpVerb.GET
        };
        return Task.FromResult(_client.Value.GetPreSignedURL(request));
    }

    /// <summary>桶探测只做一次：S3FileStorage 由宿主按单例注册，成功后不再重复 PutBucket。</summary>
    private async Task EnsureBucketOnceAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _bucketEnsured) == 1)
        {
            return;
        }
        await EnsureBucketAsync(cancellationToken);
        Volatile.Write(ref _bucketEnsured, 1);
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _client.Value.PutBucketAsync(new PutBucketRequest { BucketName = _options.Bucket }, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode is HttpStatusCode.Conflict)
        {
            // BucketAlreadyOwnedByYou：桶已存在，忽略
        }
    }
}
