using System;
using System.IO;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

/// <summary>
/// 本地存储签名下载端点：替代原匿名 /storage 静态文件挂载。
/// URL 携带 HMAC-SHA256 签名 + 过期时间（由 LocalFileStorage 生成），签名即凭证，故允许匿名；
/// 校验失败一律 404，不暴露 key 是否存在。
/// </summary>
[Area("compare")]
[Route("api/compare/storage")]
[AllowAnonymous]
public class StorageFileController : AbpController
{
    private readonly IFileStorage _fileStorage;

    public StorageFileController(IFileStorage fileStorage)
    {
        _fileStorage = fileStorage;
    }

    /// <summary>GET /api/compare/storage/file?key=...&amp;expires=...&amp;sig=...（仅 Storage:Provider=Local 时可用）</summary>
    [HttpGet("file")]
    public async Task<IActionResult> DownloadAsync([FromQuery] string key, [FromQuery] long expires, [FromQuery] string sig)
    {
        if (_fileStorage is not LocalFileStorage local)
        {
            return NotFound();
        }
        if (key.IsNullOrWhiteSpace() || !local.ValidateSignedUrl(key, expires, sig))
        {
            return NotFound();
        }

        Stream stream;
        try
        {
            stream = await local.GetAsync(key);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException)
        {
            // key 路径穿越尝试
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
