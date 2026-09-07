using Volo.Abp.BlobStoring;

namespace DredgeAI.BlobStoring;

/// <summary>
/// 扩展的 Blob 容器接口：在 ABP <see cref="IBlobContainer"/> 之上暴露
/// <see cref="IDredgeBlobProvider"/> 的 stat / 范围读取 / 前缀删除 / 预签名下载地址四个扩展能力。
/// </summary>
public interface IDredgeBlobContainer<TContainer> : IDredgeBlobContainer
    where TContainer : class
{
}

public interface IDredgeBlobContainer : IBlobContainer
{
    /// <summary>保存 blob 并记录 ContentType；overrideExisting 为 false 且已存在时抛 BlobAlreadyExistsException。</summary>
    Task SaveAsync(string name, Stream stream, string contentType, bool overrideExisting = false, CancellationToken cancellationToken = default);

    /// <summary>获取 blob 元信息；不存在时返回 null。</summary>
    Task<BlobFileInfo?> GetBlobFileInfoAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>读取字节切片 [offset, offset+length)；不存在返回 null，offset 超过 EOF 返回空流。</summary>
    Task<Stream?> GetRangeOrNullAsync(string name, long offset, long length, CancellationToken cancellationToken = default);

    /// <summary>以 prefix 为前缀删除全部匹配 blob，返回删除数量；空白前缀抛 <see cref="ArgumentException"/>。</summary>
    Task<int> DeleteByPrefixAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>预签名下载地址；不存在返回 null；FileSystem provider 经 HMAC 签名 URL 支持（需配置 BlobFileSystemSigningOptions）。</summary>
    Task<string?> GetDownloadUrlAsync(string name, int? expirySeconds = null, CancellationToken cancellationToken = default);
}
