using System;
using System.IO;
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
        await EnsureBucketAsync(cancellationToken);
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

    public async Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var response = await _client.Value.GetObjectAsync(_options.Bucket, key, cancellationToken);
        var buffer = new MemoryStream();
        await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        response.Dispose();
        return buffer;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        await _client.Value.DeleteObjectAsync(_options.Bucket, key, cancellationToken);
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
