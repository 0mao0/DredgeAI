using System;
using System.IO;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Storage;
using DredgeAI.BlobStoring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Volo.Abp;

namespace DredgeAI.BidCompare.Controllers;

/// <summary>
/// 本地存储签名下载端点：替代原匿名 /storage 静态文件挂载。
/// URL 携带 HMAC-SHA256 签名 + 过期时间（由 DredgeFileSystemBlobProvider 生成），签名即凭证，故允许匿名；
/// 校验失败一律 404，不暴露 key 是否存在。
/// </summary>
[AllowAnonymous]
[Route("api/bidcompare/storage")]
[RemoteService(Name = BidCompareRemoteServiceConsts.RemoteServiceName)]
[Area(BidCompareRemoteServiceConsts.ModuleName)]
[Tags("文件存储")]
public class StorageFileController : BidCompareController
{
    private readonly IDredgeBlobContainer<BidCompareFileContainer> _container;
    private readonly IOptions<BlobFileSystemSigningOptions> _signingOptions;

    public StorageFileController(
        IDredgeBlobContainer<BidCompareFileContainer> container,
        IOptions<BlobFileSystemSigningOptions> signingOptions)
    {
        _container = container;
        _signingOptions = signingOptions;
    }

    /// <summary>GET /api/bidcompare/storage/file?key=...&amp;expires=...&amp;sig=...（仅 Storage:Provider=Local 时可用）</summary>
    /// <param name="key">文件 key，由 Blob 提供方生成</param>
    /// <param name="expires">签名过期时间（Unix 秒）</param>
    /// <param name="sig">HMAC-SHA256 签名</param>
    /// <returns>文件流响应；签名无效或文件不存在时返回 404</returns>
    [HttpGet("file")]
    public async Task<IActionResult> DownloadAsync([FromQuery] string key, [FromQuery] long expires, [FromQuery] string sig)
    {
        var secret = _signingOptions.Value.SigningSecret;
        if (secret.IsNullOrWhiteSpace()  // S3 模式不配置签名密钥，端点关闭
            || key.IsNullOrWhiteSpace()
            || key.Contains("..", StringComparison.Ordinal)  // FilePathCalculator 无穿越防护，端点侧拦截
            || !BlobDownloadUrlSigner.Validate(secret, key, expires, sig))
        {
            return NotFound();
        }

        var stream = await _container.GetOrNullAsync(key);
        if (stream is null)
        {
            return NotFound();
        }

        return new FileStreamResult(stream, ContentTypeOf(Path.GetExtension(key)))
        {
            EnableRangeProcessing = true
        };
    }

    private static string ContentTypeOf(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".md" => "text/markdown",
        ".json" => "application/json",
        ".jsonl" => "application/x-ndjson",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "application/octet-stream"
    };
}
