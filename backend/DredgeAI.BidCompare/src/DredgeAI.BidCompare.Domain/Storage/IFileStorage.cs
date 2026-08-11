using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.Storage;

/// <summary>
/// 对象存储抽象（原始文件 / IR 包 / 导出文件）。
/// 生产实现：S3 兼容（MinIO）AWSSDK.S3；单机/开发实现：LocalFileStorage；测试实现：InMemoryFileStorage。
/// key 约定：compare/{taskId}/{docId}/origin.{ext}、compare/{taskId}/{docId}/ir.json（内部适配 IR）、
/// compare/{taskId}/{docId}/content.md、compare/{taskId}/{docId}/images/...、
/// compare/{taskId}/{docId}/raw/（AnGIneer 原始产物留档）、compare/{taskId}/exports/{jobId}.{ext}
/// </summary>
public interface IFileStorage
{
    /// <summary>上传对象，返回存储 key。</summary>
    Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>读取对象内容。调用方负责 Dispose 返回的流。</summary>
    Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>生成限时下载链接（导出文件下载用，spec §6.2）。</summary>
    Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);
}
