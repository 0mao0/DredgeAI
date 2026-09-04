using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BlobStoring;

/// <summary>
/// 扩展的 Blob Provider 接口：stat / 范围读取 / 前缀删除 / 预签名下载地址。
/// 由 <see cref="DredgeMinioBlobProvider"/> 与 <see cref="DredgeFileSystemBlobProvider"/> 实现，
/// 路径计算复用 ABP 基类（MinioBlobNameCalculator / FilePathCalculator），自动带租户前缀。
/// </summary>
/// <remarks>
/// 契约：
/// - args 中的 BlobName/ContainerName 为原始名（与 IBlobContainer 层约定一致），
///   租户前缀由实现内部经 calculator 计算，调用方无需手工拼接。
/// - 各 "OrNull" 方法：目标不存在时返回 null，不抛异常。
/// - <see cref="DeleteByPrefixAsync"/> 以 args.BlobName 为前缀删除；空白前缀抛 <see cref="ArgumentException"/>。
/// - FileSystem 实现经 HMAC 签名 URL 支持 <see cref="GetDownloadUrlAsync"/>（需配置 BlobFileSystemSigningOptions）。
/// </remarks>
public interface IDredgeBlobProvider : IBlobProvider, ITransientDependency
{
    /// <summary>
    /// 保存 blob 并记录 ContentType（Minio 写入对象元数据，预签名下载按此返回；
    /// FileSystem 忽略该参数——本地下载端点按扩展名推断 ContentType）。
    /// 覆盖语义与基类一致：args.OverrideExisting 为 false 且已存在时抛 BlobAlreadyExistsException。
    /// </summary>
    Task SaveAsync(BlobProviderSaveArgs args, string contentType);

    /// <summary>
    /// 获取 blob 元信息；不存在时返回 null。
    /// </summary>
    Task<BlobFileInfo?> GetStatOrNullAsync(BlobProviderGetArgs args);

    /// <summary>
    /// 读取 blob 的字节切片 [offset, offset+length)；blob 不存在时返回 null。
    /// offset 超过 EOF 返回空流。
    /// </summary>
    Task<Stream?> GetRangeOrNullAsync(BlobProviderGetArgs args, long offset, long length);

    /// <summary>
    /// 以 args.BlobName 为前缀删除全部匹配 blob，返回删除数量。
    /// </summary>
    Task<int> DeleteByPrefixAsync(BlobProviderDeleteArgs args);

    /// <summary>
    /// 获取预签名下载地址；blob 不存在时返回 null。FileSystem 实现经 HMAC 签名 URL 支持（需配置 BlobFileSystemSigningOptions）。
    /// </summary>
    Task<string?> GetDownloadUrlAsync(BlobProviderGetArgs args, int? expirySeconds = null);
}
